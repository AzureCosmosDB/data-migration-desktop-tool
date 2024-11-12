using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Encryption;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.CosmosExtension
{
    [Export(typeof(IDataSourceExtension))]
    public class CosmosDataSourceExtension : IDataSourceExtensionWithSettings
    {
        public string DisplayName => "Cosmos-nosql";

        public async IAsyncEnumerable<IDataItem> ReadAsync(IConfiguration config, ILogger logger, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var settings = config.Get<CosmosSourceSettings>();
            settings.Validate();

            var client = CosmosExtensionServices.CreateClient(settings, DisplayName);

            Container container;
            
            if(settings.InitClientEncryption)
            {
                container = await client.GetContainer(settings.Database, settings.Container).InitializeEncryptionAsync();
            }
            else
            {
                container = client.GetContainer(settings.Database, settings.Container);
            }

            await CosmosExtensionServices.VerifyContainerAccess(container, settings.Container, logger, cancellationToken);

            var requestOptions = new QueryRequestOptions();
            if (!string.IsNullOrEmpty(settings.PartitionKeyValue))
            {
                requestOptions.PartitionKey = new PartitionKey(settings.PartitionKeyValue);
            }

            logger.LogInformation("Reading from {Database}.{Container}", settings.Database, settings.Container);
            using FeedIterator<Dictionary<string, object?>> feedIterator = GetFeedIterator<Dictionary<string, object?>>(settings, container, requestOptions);
            while (feedIterator.HasMoreResults)
            {
                foreach (var item in await feedIterator.ReadNextAsync(cancellationToken))
                {
                    if (!settings.IncludeMetadataFields)
                    {
                        var corePropertiesOnly = new Dictionary<string, object?>(item.Where(kvp => !kvp.Key.StartsWith("_")));
                        yield return new CosmosDictionaryDataItem(corePropertiesOnly);
                    }
                    else
                    {
                        yield return new CosmosDictionaryDataItem(item);
                    }
                }
            }
        }

        private static FeedIterator<T> GetFeedIterator<T>(CosmosSourceSettings settings, Container container, QueryRequestOptions requestOptions)
        {
            if (string.IsNullOrWhiteSpace(settings.Query))
            {
                return container.GetItemQueryIterator<T>(requestOptions: requestOptions);
            }

            return container.GetItemQueryIterator<T>(settings.Query, requestOptions: requestOptions);
        }

        public IEnumerable<IDataExtensionSettings> GetSettings()
        {
            yield return new CosmosSourceSettings();
        }
    }
}