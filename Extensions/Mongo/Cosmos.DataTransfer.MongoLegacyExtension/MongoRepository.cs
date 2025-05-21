using System.Linq.Expressions;
using MongoDB.Driver;

namespace Cosmos.DataTransfer.MongoLegacyExtension;

public class MongoRepository<TDocument> : IRepository<TDocument>
{
    private readonly MongoCollection<TDocument> collection;

    public MongoRepository(MongoCollection<TDocument> collection)
    {
        this.collection = collection;
    }

    public async ValueTask Add(TDocument item)
    {
        await Task.Run(() => collection.Insert(item));
    }

    public async ValueTask AddRange(IEnumerable<TDocument> items)
    {
        await Task.Run(() => collection.InsertBatch(items));
    }

    public async ValueTask AddRange(params TDocument[] items)
    {
        await Task.Run(() => collection.InsertBatch(items));
    }

    public IQueryable<TDocument> AsQueryable()
    {
        return collection.AsQueryable();
    }
}