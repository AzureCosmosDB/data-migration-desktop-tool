using System.Linq.Expressions;

namespace Cosmos.DataTransfer.MongoLegacyExtension;

public interface IRepository<TDocument>
{
    ValueTask Add(TDocument item);
    ValueTask AddRange(IEnumerable<TDocument> items);
    ValueTask AddRange(params TDocument[] items);
    IQueryable<TDocument> AsQueryable();
}