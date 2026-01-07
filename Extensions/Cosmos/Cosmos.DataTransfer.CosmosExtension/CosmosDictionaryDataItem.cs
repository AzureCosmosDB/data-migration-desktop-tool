using Cosmos.DataTransfer.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cosmos.DataTransfer.CosmosExtension
{
    public class CosmosDictionaryDataItem : IDataItem
    {
        public IDictionary<string, object?> Items { get; }

        public CosmosDictionaryDataItem(IDictionary<string, object?> items)
        {
            Items = items;
        }

        /// <summary>
        /// Converts a JObject to a Dictionary while preserving all properties including metadata properties like $type.
        /// </summary>
        /// <remarks>
        /// Using ToObject&lt;Dictionary&gt; would filter out metadata properties because Newtonsoft.Json
        /// treats properties like $type, $id, and $ref as special metadata even when TypeNameHandling is None.
        /// </remarks>
        public static IDictionary<string, object?> JObjectToDictionary(JObject jObject)
        {
            return jObject.Properties().ToDictionary(
                p => p.Name,
                p => ConvertJTokenValue(p.Value));
        }

        /// <summary>
        /// Converts a JToken to its appropriate object representation while preserving JObject and JArray types.
        /// </summary>
        private static object? ConvertJTokenValue(JToken token)
        {
            return token.Type switch
            {
                JTokenType.Object => token, // Keep as JObject, will be converted by GetChildObject
                JTokenType.Array => token, // Keep as JArray, will be converted by GetChildObject
                _ => ((JValue)token).Value // For primitives, extract the value
            };
        }

        public IEnumerable<string> GetFieldNames()
        {
            return Items.Keys;
        }

        public object? GetValue(string fieldName)
        {
            if (!Items.TryGetValue(fieldName, out var value))
            {
                return null;
            }

            return GetChildObject(value);
        }

        private static object? GetChildObject(object? value)
        {
            if (value is JObject element)
            {
                // Use the public utility method for consistency
                var dict = JObjectToDictionary(element);
                return new CosmosDictionaryDataItem(dict);
            }
            if (value is JArray array)
            {
                return array.Select(item => {
                    if (item is JObject || item is JArray)
                    {
                        return GetChildObject(item);
                    }
                    return ((JValue)item).Value;
                }).ToList();
            }

            return value;
        }
    }
}