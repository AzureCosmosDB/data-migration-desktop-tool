using Cosmos.DataTransfer.Interfaces;

namespace Cosmos.DataTransfer.PostgresqlExtension
{
    public class PostgreDictionaryDataItem : IDataItem
    {
        public IDictionary<string, object?> Columns { get; set; }

        public PostgreDictionaryDataItem(IDictionary<string, object?> columns)
        {
            Columns = columns;
        }
        public IEnumerable<string> GetFieldNames()
        {
            return Columns.Keys;
        }

        public object? GetValue(string fieldName)
        {
            if (!Columns.TryGetValue(fieldName, out var value))
            {
                return null;
            }
            return value;
        }
    }
}
