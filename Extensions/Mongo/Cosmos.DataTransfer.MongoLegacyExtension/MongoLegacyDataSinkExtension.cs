using System.ComponentModel.Composition;
using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.MongoLegacyExtension.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

namespace Cosmos.DataTransfer.MongoLegacyExtension;
[Export(typeof(IDataSinkExtension))]
public class MongoLegacyDataSinkExtension : IDataSinkExtensionWithSettings
{
    public string DisplayName => "MongoDB-Legacy (Wire v2)";

    public async Task WriteAsync(IAsyncEnumerable<IDataItem> dataItems, IConfiguration config, IDataSourceExtension dataSource, ILogger logger, CancellationToken cancellationToken = default)
    {
        var settings = config.Get<MongoLegacySinkSettings>();
        
        if (settings == null)
        {
            logger.LogError("Failed to parse MongoDB Legacy sink settings");
            return;
        }

        if (settings.ConnectionString == null || settings.DatabaseName == null || settings.Collection == null)
        {
            logger.LogError("MongoDB Legacy sink settings missing required fields");
            return;
        }

        if (!string.IsNullOrEmpty(settings.ConnectionString) && !string.IsNullOrEmpty(settings.DatabaseName) && !string.IsNullOrEmpty(settings.Collection))
        {
            var context = new Context(settings.ConnectionString, settings.DatabaseName);
            var repo = context.GetRepository<BsonDocument>(settings.Collection);

            var batchSize = settings.BatchSize ?? 1000;

            var objects = new List<BsonDocument>();
            int itemCount = 0;
            await foreach (var item in dataItems.WithCancellation(cancellationToken))
            {
                var dict = item.BuildDynamicObjectTree();
                var bsonDoc = new BsonDocument(dict);
                
                // Map the specified field to _id if IdFieldName is provided
                if (!string.IsNullOrEmpty(settings.IdFieldName) && dict != null)
                {
                    var sourceField = item.GetFieldNames().FirstOrDefault(n => n.Equals(settings.IdFieldName, StringComparison.CurrentCultureIgnoreCase));
                    if (sourceField != null)
                    {
                        var idValue = item.GetValue(sourceField);
                        if (idValue != null)
                        {
                            bsonDoc["_id"] = BsonValue.Create(idValue);
                        }
                    }
                }
                
                objects.Add(bsonDoc);
                itemCount++;

                if (objects.Count == batchSize)
                {
                    await repo.AddRange(objects);
                    logger.LogInformation("Added {ItemCount} items to collection '{Collection}'", itemCount, settings.Collection);
                    objects.Clear();
                }
            }

            if (objects.Any())
            {
                await repo.AddRange(objects);
            }

            if (itemCount > 0)
                logger.LogInformation("Added {ItemCount} total items to collection '{Collection}'", itemCount, settings.Collection);
            else
                logger.LogWarning("No items added to collection '{Collection}'", settings.Collection);
        }
    }

    public IEnumerable<IDataExtensionSettings> GetSettings()
    {
        yield return new MongoLegacySinkSettings();
    }
}