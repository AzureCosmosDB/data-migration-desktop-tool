using System.ComponentModel.Composition;
using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.MongoExtension.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

namespace Cosmos.DataTransfer.MongoExtension;
[Export(typeof(IDataSinkExtension))]
public class MongoDataSinkExtension : IDataSinkExtensionWithSettings
{
    public string DisplayName => "MongoDB";

    public async Task WriteAsync(IAsyncEnumerable<IDataItem> dataItems, IConfiguration config, IDataSourceExtension dataSource, ILogger logger, CancellationToken cancellationToken = default)
    {
        var settings = config.Get<MongoSinkSettings>();
        settings.Validate();

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
                objects.Add(new BsonDocument(dict));
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
        yield return new MongoSinkSettings();
    }
}
