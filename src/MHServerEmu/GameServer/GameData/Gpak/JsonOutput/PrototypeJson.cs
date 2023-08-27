﻿using MHServerEmu.GameServer.GameData.Gpak.FileFormats;

namespace MHServerEmu.GameServer.GameData.Gpak.JsonOutput
{
    public class PrototypeJson
    {
        public uint Header { get; }
        public PrototypeDataJson Data { get; }

        public PrototypeJson(Prototype prototype, Dictionary<ulong, string> prototypeDict, Dictionary<ulong, string> curveDict, Dictionary<ulong, string> assetDict)
        {
            Header = prototype.Header;
            Data = new(prototype.Data, prototypeDict, curveDict, assetDict);
        }

        public class PrototypeDataJson
        {
            public byte Flags { get; }
            public string Id { get; }
            public PrototypeDataEntryJson[] Entries { get; }

            public PrototypeDataJson(PrototypeData data, Dictionary<ulong, string> prototypeDict, Dictionary<ulong, string> curveDict, Dictionary<ulong, string> assetDict)
            {
                Flags = data.Flags;
                Id = prototypeDict[data.Id];

                if (data.Entries != null)
                {
                    Entries = new PrototypeDataEntryJson[data.Entries.Length];
                    for (int i = 0; i < Entries.Length; i++)
                        Entries[i] = new(data.Entries[i], prototypeDict, curveDict, assetDict);
                }
            }

            public class PrototypeDataEntryJson
            {
                public string Id { get; }
                public byte Field1 { get; }
                public PrototypeDataEntryElementJson[] Elements { get; }
                public PrototypeDataEntryListElementJson[] ListElements { get; }

                public PrototypeDataEntryJson(PrototypeDataEntry entry, Dictionary<ulong, string> prototypeDict, Dictionary<ulong, string> curveDict, Dictionary<ulong, string> assetDict)
                {
                    Id = prototypeDict[entry.Id];
                    Field1 = entry.Field1;

                    Elements = new PrototypeDataEntryElementJson[entry.Elements.Length];
                    for (int i = 0; i < Elements.Length; i++)
                        Elements[i] = new(entry.Elements[i], prototypeDict, curveDict, assetDict);

                    ListElements = new PrototypeDataEntryListElementJson[entry.ListElements.Length];
                    for (int i = 0; i < ListElements.Length; i++)
                        ListElements[i] = new(entry.ListElements[i], prototypeDict, curveDict, assetDict);
                }

                public class PrototypeDataEntryElementJson
                {
                    public ulong Id { get; }
                    public char Type { get; }
                    public object Value { get; }

                    public PrototypeDataEntryElementJson(PrototypeDataEntryElement element, Dictionary<ulong, string> prototypeDict, Dictionary<ulong, string> curveDict, Dictionary<ulong, string> assetDict)
                    {
                        Id = element.Id;
                        Type = (char)element.Type;

                        switch (Type)
                        {
                            case 'A':
                                Value = assetDict[(ulong)element.Value];
                                break;
                            case 'C':
                                Value = curveDict[(ulong)element.Value];
                                break;
                            case 'P':
                                Value = prototypeDict[(ulong)element.Value];
                                break;
                            case 'R':
                                Value = new PrototypeDataJson((PrototypeData)element.Value, prototypeDict, curveDict, assetDict);
                                break;
                            default:
                                Value = element.Value;
                                break;
                        }
                    }
                }

                public class PrototypeDataEntryListElementJson
                {
                    public ulong Id { get; }
                    public char Type { get; }
                    public object[] Values { get; }

                    public PrototypeDataEntryListElementJson(PrototypeDataEntryListElement element, Dictionary<ulong, string> prototypeDict, Dictionary<ulong, string> curveDict, Dictionary<ulong, string> assetDict)
                    {
                        Id = element.Id;
                        Type = (char)element.Type;

                        Values = new object[element.Values.Length];
                        for (int i = 0; i < Values.Length; i++)
                        {
                            switch (Type)
                            {
                                case 'A':
                                    Values[i] = assetDict[(ulong)element.Values[i]];
                                    break;
                                case 'C':
                                    Values[i] = curveDict[(ulong)element.Values[i]];
                                    break;
                                case 'P':
                                    Values[i] = prototypeDict[(ulong)element.Values[i]];
                                    break;
                                case 'R':
                                    Values[i] = new PrototypeDataJson((PrototypeData)element.Values[i], prototypeDict, curveDict, assetDict);
                                    break;
                                default:
                                    Values[i] = element.Values[i];
                                    break;
                            }
                        }                                
                    }
                }
            }
        }
    }
}