using MongoDB.Bson;
using MongoDB.Driver;

namespace Cosmos.DataTransfer.MongoLegacyExtension;
public class Context
{
    private readonly MongoDatabase database = null!;

    public Context(string connectionString, string databaseName)
    {
        var mongoUrl = new MongoUrl(connectionString);
        var client = new MongoClient(mongoUrl);
        var server = client.GetServer();
        database = server.GetDatabase(databaseName);
    }

    public virtual IRepository<T> GetRepository<T>(string name)
    {
        return new MongoRepository<T>(database.GetCollection<T>(name));
    }

    public virtual MongoCollection<T> GetCollection<T>(string name) where T : class
    {
        return database.GetCollection<T>(name);
    }

    public virtual async Task<IList<string>> GetCollectionNamesAsync()
    {
        var names = await Task.Run(() => database.GetCollectionNames().ToList());
        return names;
    }
}