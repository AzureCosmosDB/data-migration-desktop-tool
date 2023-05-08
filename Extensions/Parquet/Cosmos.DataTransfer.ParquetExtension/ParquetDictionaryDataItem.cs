using Cosmos.DataTransfer.Interfaces;

namespace Cosmos.DataTransfer.ParquetExtension
{
    public class ParquetDictionaryDataItem : IDataItem
    {
        public IDictionary<string, object?> Items { get; set; }

        public ParquetDictionaryDataItem(IDictionary<string, object?> items)
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
            return value;
        }
    }
}
