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
                // Manually convert JObject properties to dictionary to preserve all properties including $type
                // Using ToObject<Dictionary> would filter out metadata properties like $type
                var dict = element.Properties().ToDictionary(
                    p => p.Name, 
                    p => {
                        if (p.Value is JObject || p.Value is JArray)
                        {
                            return (object?)p.Value; // Keep as JToken, will be recursively converted
                        }
                        // For primitive values, use the Value property directly
                        return ((JValue)p.Value).Value;
                    });
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