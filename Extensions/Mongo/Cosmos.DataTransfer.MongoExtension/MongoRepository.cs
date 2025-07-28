using System.Linq.Expressions;
using MongoDB.Driver;

namespace Cosmos.DataTransfer.MongoExtension;

public class MongoRepository<TDocument> : IRepository<TDocument>
{
    private readonly IMongoCollection<TDocument> collection;

    public MongoRepository(IMongoCollection<TDocument> collection)
    {
        this.collection = collection;
    }

    public async ValueTask Add(TDocument item)
    {
        await collection.InsertOneAsync(item);
    }

    public async ValueTask AddRange(IEnumerable<TDocument> items)
    {
        await collection.InsertManyAsync(items);
    }

    public async ValueTask AddRange(params TDocument[] items)
    {
        await collection.InsertManyAsync(items);
    }

    public async ValueTask Update(Expression<Func<TDocument, bool>> filter, TDocument value)
    {
        await collection.FindOneAndReplaceAsync(filter, value);
    }

    public async ValueTask Remove(Expression<Func<TDocument, bool>> filter)
    {
        await collection.DeleteOneAsync(filter);
    }

    public async ValueTask RemoveRange(Expression<Func<TDocument, bool>> filter)
    {
        await collection.DeleteManyAsync(filter);
    }

    public IQueryable<TDocument> AsQueryable()
    {
        return collection.AsQueryable();
    }

    public async IAsyncEnumerable<TDocument> FindAsync(FilterDefinition<TDocument> filter)
    {
        using var cursor = await collection.FindAsync(filter);
        while (await cursor.MoveNextAsync())
        {
            foreach (var document in cursor.Current)
            {
                yield return document;
            }
        }
    }
}