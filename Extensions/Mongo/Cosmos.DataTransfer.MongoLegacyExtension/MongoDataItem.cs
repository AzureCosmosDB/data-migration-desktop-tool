using Cosmos.DataTransfer.Interfaces;
using MongoDB.Bson;

namespace Cosmos.DataTransfer.MongoLegacyExtension;
public class MongoDataItem : IDataItem
{
    private readonly BsonDocument record;

    public MongoDataItem(BsonDocument record)
    {
        this.record = record;
    }

    public IEnumerable<string> GetFieldNames()
    {
        return record.Names;
    }

    public object? GetValue(string fieldName)
    {
        if (!record.Contains(fieldName))
            return null;
            
        var value = record[fieldName];
        return BsonTypeMapper.MapToDotNetValue(value);
    }
}