using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using MongoDB.Driver.Encryption;

namespace Cosmos.DataTransfer.MongoExtension;
public class Context
{
    private readonly IMongoDatabase database = null!;

    public Context(string connectionString, string databaseName, 
        string? keyVaultNamespace = null, Dictionary<string, IReadOnlyDictionary<string, object>>? kmsProviders = null)
    {
        var mongoConnectionUrl = new MongoUrl(connectionString);
        var mongoClientSettings = MongoClientSettings.FromUrl(mongoConnectionUrl);
        mongoClientSettings.ClusterConfigurator = cb => {
            cb.Subscribe<CommandStartedEvent>(e => {
                System.Diagnostics.Debug.WriteLine($"{e.CommandName} - {e.Command.ToJson()}");
            });
        };

        if(!string.IsNullOrEmpty(keyVaultNamespace) && kmsProviders?.Count != 0)
        {
            var autoEncryptionOptions = new AutoEncryptionOptions(
                        keyVaultNamespace: CollectionNamespace.FromFullName(keyVaultNamespace),
                        kmsProviders: kmsProviders,
                        bypassAutoEncryption: true);
            mongoClientSettings.AutoEncryptionOptions = autoEncryptionOptions;
        }

        var client = new MongoClient(mongoClientSettings);
        database = client.GetDatabase(databaseName);
    }

    public virtual IRepository<T> GetRepository<T>(string name)
    {
        return new MongoRepository<T>(database.GetCollection<T>(name));
    }

    public virtual IMongoCollection<T> GetCollection<T>(string name)
    {
        return database.GetCollection<T>(name);
    }

    public virtual async Task RenameCollectionAsync(string originalName, string newName)
    {
        await database.RenameCollectionAsync(originalName, newName);
    }

    public virtual async Task DropCollectionAsync(string name)
    {
        await database.DropCollectionAsync(name);
    }

    public virtual async Task<IList<string>> GetCollectionNamesAsync()
    {
        var names = await database.ListCollectionNamesAsync();
        return names.ToList();
    }
}
