using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.MongoLegacyExtension.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

namespace Cosmos.DataTransfer.MongoLegacyExtension;
[Export(typeof(IDataSourceExtension))]
internal class MongoLegacyDataSourceExtension : IDataSourceExtensionWithSettings
{
    public string DisplayName => "MongoDB-Legacy (Wire v2)";

    public async IAsyncEnumerable<IDataItem> ReadAsync(IConfiguration config, ILogger logger, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var settings = config.Get<MongoLegacySourceSettings>();
        
        if (settings == null)
        {
            logger.LogError("Failed to parse MongoDB Legacy source settings");
            yield break;
        }

        if (settings.ConnectionString == null || settings.DatabaseName == null)
        {
            logger.LogError("MongoDB Legacy source settings missing required fields");
            yield break;
        }

        if (!string.IsNullOrEmpty(settings.ConnectionString) && !string.IsNullOrEmpty(settings.DatabaseName))
        {
            var context = new Context(settings.ConnectionString, settings.DatabaseName);

            var collectionNames = !string.IsNullOrEmpty(settings.Collection)
                ? new List<string> { settings.Collection }
                : await context.GetCollectionNamesAsync();

            foreach (var collection in collectionNames)
            {
                await foreach (var item in EnumerateCollectionAsync(context, collection, logger).WithCancellation(cancellationToken))
                {
                    yield return item;
                }
            }
        }
    }

    public async IAsyncEnumerable<IDataItem> EnumerateCollectionAsync(Context context, string collectionName, ILogger logger)
    {
        logger.LogInformation("Reading collection '{Collection}' using legacy MongoDB driver", collectionName);
        var collection = context.GetRepository<BsonDocument>(collectionName);
        int itemCount = 0;
        foreach (var record in await Task.Run(() => collection.AsQueryable()))
        {
            yield return new MongoDataItem(record);
            itemCount++;
        }
        if (itemCount > 0)
            logger.LogInformation("Read {ItemCount} items from collection '{Collection}'", itemCount, collectionName);
        else
            logger.LogWarning("No items read from collection '{Collection}'", collectionName);
    }

    public IEnumerable<IDataExtensionSettings> GetSettings()
    {
        yield return new MongoLegacySourceSettings();
    }
}