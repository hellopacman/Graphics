using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor.Rendering;
using UnityEngine;

namespace UnityEditor.ShaderGraph.Serialization
{
    static class MultiJsonInternal
    {
        public class UnknownJsonObject : JsonObject
        {
            public string typeInfo;
            public string jsonData;
            public JsonData<JsonObject> castedObject;

            public UnknownJsonObject(string typeInfo)
            {
                this.typeInfo = typeInfo;
            }
            public override void Deserailize(string typeInfo, string jsonData)
            {
                this.jsonData = jsonData;
            }

            public override string Serialize()
            {
                return jsonData;
            }

            public override void OnAfterDeserialize(string json)
            {
                if (castedObject.value != null)
                {
                    Enqueue(castedObject, json.Trim());
                }
            }

            public override void OnAfterMultiDeserialize(string json)
            {
                if(castedObject.value == null)
                {
                    //Never got casted so nothing ever reffed this object
                    //likely that some other unknown json object had a ref
                    //to this thing. Need to include it in the serialization
                    //step of the object still.
                    if(jsonBlobs.TryGetValue(currentRoot.objectId, out var blobs))
                    {
                        blobs[objectId] = jsonData.Trim();
                    }
                    else
                    {
                        var lookup = new Dictionary<string, string>();
                        lookup[objectId] = jsonData.Trim();
                        jsonBlobs.Add(currentRoot.objectId, lookup) ;
                    }
                }
            }

            public override T CastTo<T>()
            {
                if (castedObject.value != null)
                    return castedObject.value.CastTo<T>();

                Type t = typeof(T);
                if(t == typeof(AbstractMaterialNode) || t.IsSubclassOf(typeof(AbstractMaterialNode)))
                {
                    UnknownNodeType unt = new UnknownNodeType(jsonData);
                    valueMap[objectId] = unt;
                    s_ObjectIdField.SetValue(unt, objectId);
                    castedObject = unt;
                    return unt.CastTo<T>();
                }
                else if(t == typeof(Target) || t.IsSubclassOf(typeof(Target)))
                {
                    UnknownTargetType utt = new UnknownTargetType(typeInfo, jsonData);
                    valueMap[objectId] = utt;
                    s_ObjectIdField.SetValue(utt, objectId);
                    castedObject = utt;
                    return utt.CastTo<T>();
                }
                else if(t == typeof(SubTarget) || t.IsSubclassOf(typeof(SubTarget)))
                {
                    UnknownSubTargetType ustt = new UnknownSubTargetType(typeInfo,jsonData);
                    valueMap[objectId] = ustt;
                    s_ObjectIdField.SetValue(ustt, objectId);
                    castedObject = ustt;
                    return ustt.CastTo<T>();
                }
                else
                {
                    Debug.LogError($"Unable to evaluate type {typeInfo} : {jsonData}");
                }
                return null;
            }
        }

        public class UnknownTargetType : Target
        {
            public string jsonData;
            public UnknownTargetType() : base ()
            {
                isHidden = true;
            }

            public UnknownTargetType(string displayName, string jsonData)
            {
                this.displayName = displayName;
                isHidden = false;
                this.jsonData = jsonData;
            }

            public override void Deserailize(string typeInfo, string jsonData)
            {
                this.jsonData = jsonData;
                base.Deserailize(typeInfo, jsonData);
            }
            public override string Serialize()
            {
                return jsonData.Trim();
            }
            public override void GetActiveBlocks(ref TargetActiveBlockContext context)
            {
            }

            public override void GetFields(ref TargetFieldContext context)
            {
            }

            public override void GetPropertiesGUI(ref TargetPropertyGUIContext context, Action onChange, Action<string> registerUndo)
            {
            }

            public override bool IsActive() => false;

            public override void Setup(ref TargetSetupContext context)
            {
            }
        }

        private class UnknownSubTargetType : SubTarget
        {
            public string jsonData;
            public UnknownSubTargetType() : base()
            {
                isHidden = true;
            }
            public UnknownSubTargetType(string displayName, string jsonData) : base()
            {
                isHidden = false;
                this.displayName = displayName;
                this.jsonData = jsonData;
            }

            public override void Deserailize(string typeInfo, string jsonData)
            {
                this.jsonData = jsonData;
                base.Deserailize(typeInfo, jsonData);
            }

            public override string Serialize()
            {
                return jsonData.Trim();
            }

            internal override Type targetType => typeof(UnknownTargetType);

            public override void GetActiveBlocks(ref TargetActiveBlockContext context)
            {
            }

            public override void GetFields(ref TargetFieldContext context)
            {
            }

            public override void GetPropertiesGUI(ref TargetPropertyGUIContext context, Action onChange, Action<string> registerUndo)
            {
            }

            public override bool IsActive() => false;

            public override void Setup(ref TargetSetupContext context)
            {
            }
        }

        class UnknownNodeType : AbstractMaterialNode
        {
            public string jsonData;

            public UnknownNodeType() : base()
            {
                jsonData = null;
                isValid = false;
                isActive = false;
            }
            public UnknownNodeType(string jsonData) 
            {
                this.jsonData = jsonData;
                isValid = false;
                isActive = false;
            }

            public override void OnAfterDeserialize(string json)
            {
                jsonData = json;
                base.OnAfterDeserialize(json);
            }

            public override string Serialize()
            {
                EnqueSlotsForSerialization();
                return jsonData.Trim();
            }

            public override void ValidateNode()
            {
                isActive = false;
                isValid = false;
                owner.AddValidationError(objectId, "This node type could not be found. No function will be generated in the shader.", ShaderCompilerMessageSeverity.Warning);
            }
        }

        static readonly Dictionary<string, Type> k_TypeMap = CreateTypeMap();

        internal static bool isDeserializing;

        internal static readonly Dictionary<string, JsonObject> valueMap = new Dictionary<string, JsonObject>();

        static List<MultiJsonEntry> s_Entries;

        internal static bool isSerializing;

        internal static readonly List<JsonObject> serializationQueue = new List<JsonObject>();

        internal static readonly HashSet<string> serializedSet = new HashSet<string>();

        static JsonObject currentRoot = null;

        static Dictionary<string, Dictionary<string, string>>jsonBlobs = new Dictionary<string, Dictionary<string,string>>();

        static Dictionary<string, Type> CreateTypeMap()
        {
            var map = new Dictionary<string, Type>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<JsonObject>())
            {
                if (type.FullName != null)
                {
                    map[type.FullName] = type;
                }
            }

            foreach (var type in TypeCache.GetTypesWithAttribute(typeof(FormerNameAttribute)))
            {
                if (type.IsAbstract || !typeof(JsonObject).IsAssignableFrom(type))
                {
                    continue;
                }

                foreach (var attribute in type.GetCustomAttributes(typeof(FormerNameAttribute), false))
                {
                    var legacyAttribute = (FormerNameAttribute)attribute;
                    map[legacyAttribute.fullName] = type;
                }
            }

            return map;
        }

        public static Type ParseType(string typeString)
        {
            k_TypeMap.TryGetValue(typeString, out var type);
            return type;
        }

        public static List<MultiJsonEntry> Parse(string str)
        {
            var result = new List<MultiJsonEntry>();
            const string separatorStr = "\n\n";
            var startIndex = 0;
            var raw = new FakeJsonObject();

            while (startIndex < str.Length)
            {
                var jsonBegin = str.IndexOf("{", startIndex, StringComparison.Ordinal);
                if (jsonBegin == -1)
                {
                    break;
                }

                var jsonEnd = str.IndexOf(separatorStr, jsonBegin, StringComparison.Ordinal);
                if (jsonEnd == -1)
                {
                    jsonEnd = str.IndexOf("\n\r\n", jsonBegin, StringComparison.Ordinal);
                    if (jsonEnd == -1)
                    {
                        jsonEnd = str.LastIndexOf("}", StringComparison.Ordinal) + 1;
                    }
                }

                var json = str.Substring(jsonBegin, jsonEnd - jsonBegin);

                JsonUtility.FromJsonOverwrite(json, raw);
                if (startIndex != 0 && string.IsNullOrWhiteSpace(raw.type))
                {
                    throw new InvalidOperationException($"Type is null or whitespace in JSON:\n{json}");
                }

                result.Add(new MultiJsonEntry(raw.type, raw.id, json));
                raw.Reset();

                startIndex = jsonEnd + separatorStr.Length;
            }

            return result;
        }

        public static void Enqueue(JsonObject jsonObject, string json)
        {
            if (s_Entries == null)
            {
                throw new InvalidOperationException("Can only Enqueue during JsonObject.OnAfterDeserialize.");
            }

            valueMap.Add(jsonObject.objectId, jsonObject);
            s_Entries.Add(new MultiJsonEntry(jsonObject.GetType().FullName, jsonObject.objectId, json));
        }

        public static bool CreateInstance(string typeString, out JsonObject output)
        {
            output = null;
            if (!k_TypeMap.TryGetValue(typeString, out var type))
            {
                output = new UnknownJsonObject(typeString);
                return false;
            }
            output = (JsonObject)Activator.CreateInstance(type, true);
            return true; 
        }

        private static FieldInfo s_ObjectIdField =
            typeof(JsonObject).GetField("m_ObjectId", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void Deserialize(JsonObject root, List<MultiJsonEntry> entries, bool rewriteIds)
        {
            if (isDeserializing)
            {
                throw new InvalidOperationException("Nested MultiJson deserialization is not supported.");
            }

            try
            {
                isDeserializing = true;
                currentRoot = root;
                for (var index = 0; index < entries.Count; index++)
                {
                    var entry = entries[index];
                    try
                    {
                        JsonObject value = null;
                        bool tryDeserialize = true;
                        if(index == 0)
                        {
                            value = root;
                        }
                        else
                        {
                            if(!CreateInstance(entry.type, out value))
                            {
                                entries[index] = new MultiJsonEntry(entry.type, entry.id, entry.json, false);
                            }
                        }

                        var id = entry.id;

                        if (id != null)
                        {
                            // Need to make sure that references looking for the old ID will find it in spite of
                            // ID rewriting.
                            valueMap[id] = value;
                        }

                        if (rewriteIds || entry.id == null)
                        {
                            id = value.objectId;
                            entries[index] = new MultiJsonEntry(entry.type, id, entry.json, tryDeserialize);
                            valueMap[id] = value;
                        }

                        s_ObjectIdField.SetValue(value, id);
                    }
                    catch (Exception e)
                    {
                        // External code could throw exceptions, but we don't want that to fail the whole thing.
                        // Potentially, the fallback type should also be used here.
                        Debug.LogException(e);
                    }
                }

                s_Entries = entries;

                // Not a foreach because `entries` can be populated by calls to `Enqueue` as we go.
                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    try
                    {
                        var value = valueMap[entry.id];
                        value.Deserailize(entry.type, entry.json);
                        // Set ID again as it could be overwritten from JSON.
                        s_ObjectIdField.SetValue(value, entry.id);
                        value.OnAfterDeserialize(entry.json);
                    }
                    catch (Exception e)
                    {
                        if(!String.IsNullOrEmpty(entry.id))
                        {
                            var value = valueMap[entry.id];
                            if(value != null)
                            {
                                Debug.LogError($"Exception thrown while deserialize object of type {entry.type}: {e.Message}");
                            }
                        }
                        Debug.LogException(e);
                    }
                }

                s_Entries = null;

                foreach (var entry in entries)
                {
                    try
                    {
                        var value = valueMap[entry.id];
                        value.OnAfterMultiDeserialize(entry.json);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
            finally
            {
                valueMap.Clear();
                currentRoot = null;
                isDeserializing = false;
            }
        }

        public static string Serialize(JsonObject mainObject)
        {
            if (isSerializing)
            {
                throw new InvalidOperationException("Nested MultiJson serialization is not supported.");
            }

            try
            {
                isSerializing = true;

                serializedSet.Add(mainObject.objectId);
                serializationQueue.Add(mainObject);

                var idJsonList = new List<(string, string)>();

                // Not a foreach because the queue is populated by `JsonData<T>`s as we go.
                for (var i = 0; i < serializationQueue.Count; i++)
                {
                    var value = serializationQueue[i];
                    var json = value.Serialize();
                    idJsonList.Add((value.objectId, json));
                }

                if(jsonBlobs.TryGetValue(mainObject.objectId, out var blobs))
                {
                    foreach(var blob in blobs)
                    {
                        if(!idJsonList.Contains((blob.Key, blob.Value)))
                            idJsonList.Add((blob.Key, blob.Value));
                    }
                }



                idJsonList.Sort((x, y) =>
                    // Main object needs to be placed first
                    x.Item1 == mainObject.objectId ? -1 :
                    y.Item1 == mainObject.objectId ? 1 :
                    // We sort everything else by ID to consistently maintain positions in the output
                    x.Item1.CompareTo(y.Item1));

                var sb = new StringBuilder();
                foreach (var (id, json) in idJsonList)
                {
                    sb.AppendLine(json);
                    sb.AppendLine();
                }

                return sb.ToString();
            }
            finally
            {
                serializationQueue.Clear();
                serializedSet.Clear();
                isSerializing = false;
            }
        }

        public static void PopulateValueMap(JsonObject mainObject)
        {
            if (isSerializing)
            {
                throw new InvalidOperationException("Nested MultiJson serialization is not supported.");
            }

            try
            {
                isSerializing = true;

                serializedSet.Add(mainObject.objectId);
                serializationQueue.Add(mainObject);

                // Not a foreach because the queue is populated by `JsonRef<T>`s as we go.
                for (var i = 0; i < serializationQueue.Count; i++)
                {
                    var value = serializationQueue[i];
                    value.Serialize();
                    valueMap[value.objectId] = value;
                }
            }
            finally
            {
                serializationQueue.Clear();
                serializedSet.Clear();
                isSerializing = false;
            }
        }

        public static bool TryGetType(string typeName, out Type type)
        {
            type = null;
            return k_TypeMap.TryGetValue(typeName, out type);
            
        }
    }
}
