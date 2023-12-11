﻿using Gazillion;
using Google.ProtocolBuffers;
using MHServerEmu.Common.Extensions;
using MHServerEmu.Common.Logging;
using MHServerEmu.Games.Common;
using MHServerEmu.Games.Entities.Avatars;
using MHServerEmu.Games.Entities.Items;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Calligraphy;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.Properties;
using MHServerEmu.Games.Regions;
using MHServerEmu.Networking;

namespace MHServerEmu.Games.Entities
{
    public class EntityManager
    {
        private static readonly Logger Logger = LogManager.CreateLogger();

        // Hardcoded messages we use for loading
        private static readonly NetMessageEntityCreate PlayerMessage = NetMessageEntityCreate.ParseFrom(PacketHelper.LoadMessagesFromPacketFile("NetMessageEntityCreatePlayer.bin")[0].Payload);
        private static readonly NetMessageEntityCreate[] AvatarMessages = PacketHelper.LoadMessagesFromPacketFile("NetMessageEntityCreateAvatars.bin").Select(message => NetMessageEntityCreate.ParseFrom(message.Payload)).ToArray();

        private readonly Dictionary<ulong, Entity> _entityDict = new();

        private ulong _nextEntityId = 1000;
        private ulong _nextReplicationId = 50000;
        private ulong GenEntityId() { return _nextEntityId++; }
        private ulong GenReplicationId() { return _nextReplicationId++; }
        public ulong GetLastEntityId() { return _nextEntityId; }

        public WorldEntity Waypoint { get; }

        public EntityManager()
        {
            // Initialize a waypoint entity
            EntityBaseData waypointBaseData = new(ByteString.CopyFrom(Convert.FromHexString("200C839F01200020")));
            Waypoint = new(waypointBaseData, ByteString.CopyFrom(Convert.FromHexString("20F4C10206000000CD80018880FCFF99BF968110CCC00202CC800302CD40D58280DE868098044DA1A1A4FE0399C00183B8030000000000")));

            // minihack: force default player and avatar entity message initialization on construction
            // so that there isn't a lag when a player logs in for the first time after the server starts
            bool playerMessageIsEmpty = PlayerMessage == null;
            bool avatarMessagesIsEmpty = AvatarMessages == null;
        }

        public WorldEntity CreateWorldEntity(ulong regionId, ulong prototypeId, Vector3 position, Vector3 orientation,
            int health, int mapAreaId, int healthMaxOther, int mapCellId, ulong contextAreaRef, bool requiresEnterGameWorld, bool OverrideSnapToFloor = false)
        {
            EntityBaseData baseData = (requiresEnterGameWorld == false)
                ? new EntityBaseData(GenEntityId(), prototypeId, position, orientation, OverrideSnapToFloor)
                : new EntityBaseData(GenEntityId(), prototypeId, null, null);

            WorldEntity worldEntity = new(baseData, GenReplicationId(), position, health, mapAreaId, healthMaxOther, regionId, mapCellId, contextAreaRef);
            worldEntity.RegionId = regionId;
            _entityDict.Add(baseData.EntityId, worldEntity);
            return worldEntity;
        }

        public WorldEntity CreateWorldEntityEnemy(ulong regionId, ulong prototypeId, Vector3 position, Vector3 orientation,
            int health, int mapAreaId, int healthMaxOther, int mapCellId, ulong contextAreaRef, bool requiresEnterGameWorld,
            int CombatLevel, int CharacterLevel)
        {
            EntityBaseData baseData = (requiresEnterGameWorld == false)
                ? new EntityBaseData(GenEntityId(), prototypeId, position, orientation)
                : new EntityBaseData(GenEntityId(), prototypeId, null, null);

            WorldEntity worldEntity = new(baseData, GenReplicationId(), position, health, mapAreaId, healthMaxOther, regionId, mapCellId, contextAreaRef);
            worldEntity.RegionId = regionId;
            _entityDict.Add(baseData.EntityId, worldEntity);

            worldEntity.PropertyCollection.List.Add(new(PropertyEnum.CharacterLevel, CharacterLevel));
            worldEntity.PropertyCollection.List.Add(new(PropertyEnum.CombatLevel, CombatLevel)); // zero effect

            return worldEntity;
        }

        public WorldEntity CreateWorldEntityEmpty(ulong regionId, ulong prototypeId, Vector3 position, Vector3 orientation)
        {
            EntityBaseData baseData = new EntityBaseData(GenEntityId(), prototypeId, position, orientation);
            WorldEntity worldEntity = new(baseData, 1, GenReplicationId());
            worldEntity.RegionId = regionId;
            _entityDict.Add(baseData.EntityId, worldEntity);
            return worldEntity;
        }

        public Item CreateInvItem(ulong itemProto, InventoryLocation invLoc, ulong rarity, int itemLevel, float itemVariation, int seed, AffixSpec[] affixSpec, bool isNewItem) {

            EntityBaseData baseData = new ()
            {
                ReplicationPolicy = 4,
                EntityId = GenEntityId(),
                PrototypeId = itemProto,
                Flags = 96u.ToBoolArray(16), // 5 6
                InterestPolicies = 4,
                LocFlags = 0u.ToBoolArray(16),
                LocomotionState = new(0f),
                InvLoc = invLoc
            };

            if (isNewItem)
            {
                baseData.Flags[7] = true;
                baseData.InvLocPrev = new(0, 0, 0xFFFFFFFF); // -1
            }                

            ulong defRank = 15168672998566398820; // Popcorn           
            ItemSpec itemSpec = new(itemProto, rarity, itemLevel, 0, affixSpec, seed, 0);
            Item item = new(baseData, GenReplicationId(), defRank, itemLevel, rarity, itemVariation, itemSpec);
            _entityDict.Add(baseData.EntityId, item);
            return item;
        }

        public Transition SpawnDirectTeleport(ulong regionPrototype, ulong prototypeId, Vector3 position, Vector3 orientation,
            int mapAreaId, ulong regionId, int mapCellId, ulong contextAreaRef, bool requiresEnterGameWorld,
            ulong targetPrototype, bool OverrideSnapToFloor)
        {
            EntityBaseData baseData = (requiresEnterGameWorld == false)
                ? new EntityBaseData(GenEntityId(), prototypeId, position, orientation, OverrideSnapToFloor)
                : new EntityBaseData(GenEntityId(), prototypeId, null, null);
            
            PrototypeEntry regionConnectionTarget = targetPrototype.GetPrototype().GetEntry(BlueprintId.RegionConnectionTarget);

            ulong cell = regionConnectionTarget.GetFieldDef(FieldId.Cell);
            if (cell != 0) cell = GameDatabase.GetPrototypeRefByName(GameDatabase.GetAssetName(cell));

            ulong targetRegion = regionConnectionTarget.GetFieldDef(FieldId.Region);
            // Logger.Debug($"SpawnDirectTeleport {targetRegion}");
            if (targetRegion == 0) { // get Parent value
                PrototypeEntry parentTarget = targetPrototype.GetPrototype().ParentId.GetPrototype().GetEntry(BlueprintId.RegionConnectionTarget);
                if (parentTarget != null) targetRegion = parentTarget.GetFieldDef(FieldId.Region);
            }

            if (RegionManager.IsRegionAvailable((RegionPrototypeId)targetRegion) == false) // TODO: change region test
                targetRegion = regionPrototype;

            int type = 1; // default teleport
            if (targetRegion != regionPrototype) type = 2; // region teleport

            Destination destination = new()
            {
                Type = type,
                Region = targetRegion,
                Area = regionConnectionTarget.GetFieldDef(FieldId.Area),
                Cell = cell,
                Entity = (ulong)regionConnectionTarget.GetField(FieldId.Entity).Value,
                Name = "",
                NameId = regionConnectionTarget.GetFieldDef(FieldId.Name),
                Target = targetPrototype,
                Position = new()
            };

            Transition transition = new(baseData, GenReplicationId(), regionId, mapAreaId, mapCellId, contextAreaRef, position, destination);
            transition.RegionId = regionId;
            _entityDict.Add(baseData.EntityId, transition);

            return transition;
        }

        public bool DestroyEntity(ulong entityId)
        {
            if (_entityDict.TryGetValue(entityId, out _) == false)
            {
                Logger.Warn($"Failed to remove entity id {entityId}: entity does not exist");
                return false;
            }

            _entityDict.Remove(entityId);
            return true;
        }

        public Entity GetEntityById(ulong entityId) => _entityDict[entityId];
        public Entity GetEntityByPrototypeId(ulong prototype) => _entityDict.Values.FirstOrDefault(entity => entity.BaseData.PrototypeId == prototype);
        public Entity GetEntityByPrototypeIdFromRegion(ulong prototype, ulong regionId)
        {
            return _entityDict.Values.FirstOrDefault(entity => entity.BaseData.PrototypeId == prototype && entity.RegionId == regionId);
        }
        public Entity FindEntityByDestination(Destination destination, ulong regionId)
        {
            foreach (KeyValuePair<ulong, Entity> entity in _entityDict)
            {
                if (entity.Value.BaseData.PrototypeId == destination.Entity && entity.Value.RegionId == regionId)
                {
                    if (destination.Area == 0) return entity.Value;
                    Property property = entity.Value.PropertyCollection.GetPropertyByEnum(PropertyEnum.ContextAreaRef);
                    ulong area = (ulong)property.Value.Get();
                    if (area == destination.Area)
                        return entity.Value;
                }                
            }
            return null;
        }

        public bool TryGetEntityById(ulong entityId, out Entity entity) => _entityDict.TryGetValue(entityId, out entity);
        public ulong GetPropertyCollectionReplicationId(ulong entityId) => _entityDict[entityId].PropertyCollection.ReplicationId;
        public bool TryGetPropertyCollectionReplicationId(ulong entityId, out ulong replicationId)
        {
            if (_entityDict.TryGetValue(entityId, out Entity entity))
            {
                replicationId = entity.PropertyCollection.ReplicationId;
                return true;
            }

            replicationId = 0;
            return false;
        }

        public WorldEntity[] GetWorldEntitiesForRegion(ulong regionId)
        {
            IEnumerable<Entity> entities = _entityDict.Values.Where(entity => entity.RegionId == regionId).ToArray();
            return entities.Select(entity => (WorldEntity)entity).ToArray();
        }

        public Player GetDefaultPlayerEntity()
        {
            EntityBaseData baseData = new(PlayerMessage.BaseData);
            return new(baseData, PlayerMessage.ArchiveData);
        }

        public Avatar[] GetDefaultAvatarEntities()
        {
            Avatar[] avatars = new Avatar[AvatarMessages.Length];

            for (int i = 0; i < avatars.Length; i++)
            {
                EntityBaseData baseData = new(AvatarMessages[i].BaseData);
                avatars[i] = new(baseData, AvatarMessages[i].ArchiveData);
            }

            return avatars;
        }

        public IEnumerable<Entity> GetCellEntities(Cell cell)
        {
            return _entityDict.Values.Where(entity => entity is WorldEntity worldEntity && worldEntity.Cell == cell);
        }

    }
}
