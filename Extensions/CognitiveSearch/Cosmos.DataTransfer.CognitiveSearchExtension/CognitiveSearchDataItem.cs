using Cosmos.DataTransfer.Interfaces;
using System.Text.Json;

namespace Cosmos.DataTransfer.CognitiveSearchExtension
{
    public class CognitiveSearchDataItem : IDataItem
    {
        public JsonElement JsonElement { get; }

        public CognitiveSearchDataItem(JsonElement jsonElement)
        {
            JsonElement = jsonElement;
        }

        public IEnumerable<string> GetFieldNames()
        {
            return JsonElement.EnumerateObject().Where(prop => prop.Name != "@search.score").Select(prop => prop.Name);
        }

        public object? GetValue(string fieldName)
        {
            if (!JsonElement.TryGetProperty(fieldName, out JsonElement value))
            {
                return null;
            }

            return GetTypedValue(value);
        }

        private static object? GetTypedValue(JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.Number => jsonElement.GetDecimal(),
                JsonValueKind.String => jsonElement.GetString(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Array => jsonElement.EnumerateArray().Select(item => GetTypedValue(item)).ToList(),
                _ => new CognitiveSearchDataItem(jsonElement)
            };
        }
    }
}
