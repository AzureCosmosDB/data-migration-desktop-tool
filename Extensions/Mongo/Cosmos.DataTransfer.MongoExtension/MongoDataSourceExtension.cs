using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.MongoExtension.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Cosmos.DataTransfer.MongoExtension;
[Export(typeof(IDataSourceExtension))]
internal class MongoDataSourceExtension : IDataSourceExtensionWithSettings
{
    public string DisplayName => "MongoDB";

    public async IAsyncEnumerable<IDataItem> ReadAsync(IConfiguration config, ILogger logger, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var settings = config.Get<MongoSourceSettings>();
        settings.Validate();

        if (!string.IsNullOrEmpty(settings.ConnectionString) && !string.IsNullOrEmpty(settings.DatabaseName))
        {
            var context = new Context(settings.ConnectionString!, settings.DatabaseName!, settings.KeyVaultNamespace, settings.KMSProviders);

            var collectionNames = !string.IsNullOrEmpty(settings.Collection)
                ? new List<string> { settings.Collection }
                : await context.GetCollectionNamesAsync();

            foreach (var collection in collectionNames)
            {
                await foreach (var item in EnumerateCollectionAsync(context, collection, settings.Query, logger).WithCancellation(cancellationToken))
                {
                    yield return item;
                }
            }
        }
    }

    public async IAsyncEnumerable<IDataItem> EnumerateCollectionAsync(Context context, string collectionName, string? query, ILogger logger)
    {
        logger.LogInformation("Reading collection '{Collection}'", collectionName);
        var collection = context.GetRepository<BsonDocument>(collectionName);
        int itemCount = 0;

        IAsyncEnumerable<BsonDocument> documents;
        
        if (!string.IsNullOrWhiteSpace(query))
        {
            logger.LogInformation("Applying query filter to collection '{Collection}': {Query}", collectionName, query);
            documents = GetQueryDocumentsAsync(collection, query, collectionName, logger);
        }
        else
        {
            logger.LogInformation("No query filter specified for collection '{Collection}', reading all documents", collectionName);
            // Use existing queryable approach when no filter is specified
            documents = GetAllDocumentsAsync(collection);
        }

        await foreach (var record in documents)
        {
            yield return new MongoDataItem(record);
            itemCount++;
        }

        if (itemCount > 0)
            logger.LogInformation("Read {ItemCount} items from collection '{Collection}'", itemCount, collectionName);
        else
            logger.LogWarning("No items read from collection '{Collection}'", collectionName);
    }

    private async IAsyncEnumerable<BsonDocument> GetAllDocumentsAsync(IRepository<BsonDocument> collection)
    {
        foreach (var record in await Task.Run(() => collection.AsQueryable()))
        {
            yield return record;
        }
    }

    private async IAsyncEnumerable<BsonDocument> GetQueryDocumentsAsync(IRepository<BsonDocument> collection, string query, string collectionName, ILogger logger)
    {
        // Handle query as either a file path or direct JSON
        string queryJson;
        try
        {
            if (File.Exists(query))
            {
                logger.LogInformation("Reading query from file: {QueryFile}", query);
                queryJson = await File.ReadAllTextAsync(query);
            }
            else
            {
                logger.LogInformation("Treating query input as direct JSON string (file does not exist): {Query}", query);
                queryJson = query;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading query for collection '{Collection}': {Query}", collectionName, query);
            throw;
        }

        // Parse JSON to BsonDocument and create filter
        BsonDocument filterDocument;
        try
        {
            filterDocument = BsonDocument.Parse(queryJson);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error parsing query JSON for collection '{Collection}': {Query}", collectionName, queryJson);
            throw;
        }

        var filter = new BsonDocumentFilterDefinition<BsonDocument>(filterDocument);
        
        await foreach (var record in collection.FindAsync(filter))
        {
            yield return record;
        }
    }

    public IEnumerable<IDataExtensionSettings> GetSettings()
    {
        yield return new MongoSourceSettings();
    }
}
