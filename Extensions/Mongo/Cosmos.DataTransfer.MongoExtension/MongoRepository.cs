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

    public async IAsyncEnumerable<TDocument> FindAsync(FilterDefinition<TDocument> filter, int? batchSize = null)
    {
        var findOptions = new FindOptions<TDocument, TDocument>();
        // Only apply batch size if it's provided and positive; invalid values are silently ignored
        // to maintain backward compatibility and prevent exceptions during data migration
        if (batchSize.HasValue && batchSize.Value > 0)
        {
            findOptions.BatchSize = batchSize.Value;
        }
        
        using var cursor = await collection.FindAsync(filter, findOptions);
        while (await cursor.MoveNextAsync())
        {
            foreach (var document in cursor.Current)
            {
                yield return document;
            }
        }
    }
}