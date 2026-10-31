using System.Collections;
using MongoDB.Bson;

namespace Cosmos.DataTransfer.MongoExtension;

public static class MongoBsonMapper
{
    public static BsonDocument ToDocument(IDictionary<string, object?>? values)
    {
        return values == null
            ? new BsonDocument()
            : new BsonDocument(values.Select(pair => new BsonElement(pair.Key, ToValue(pair.Value))));
    }

    public static BsonValue ToValue(object? value)
    {
        return value switch
        {
            // Driver 2.x used CSharpLegacy for CLR Guids. Driver 3 requires an explicit representation.
            Guid guid => new BsonBinaryData(guid, GuidRepresentation.CSharpLegacy),
            BsonValue bson => bson,
            IDictionary<string, object?> document => ToDocument(document),
            IDictionary dictionary => new BsonDocument(dictionary.Cast<DictionaryEntry>()
                .Select(entry => new BsonElement((string)entry.Key, ToValue(entry.Value)))),
            IEnumerable array when value is not string and not byte[] =>
                new BsonArray(array.Cast<object?>().Select(ToValue)),
            _ => BsonValue.Create(value)
        };
    }

    public static object? ToDotNetValue(BsonValue value)
    {
        return value switch
        {
            BsonBinaryData { SubType: BsonBinarySubType.UuidLegacy } binary =>
                binary.ToGuid(GuidRepresentation.CSharpLegacy),
            BsonDocument document => document.ToDictionary(element => element.Name, element => ToDotNetValue(element.Value)),
            BsonArray array => array.Select(ToDotNetValue).ToList(),
            _ => BsonTypeMapper.MapToDotNetValue(value)
        };
    }
}
