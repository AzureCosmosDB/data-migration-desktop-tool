using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using Azure;
using Azure.Identity;
using Azure.Data.Tables;
using Cosmos.DataTransfer.AzureTableAPIExtension.Data;
using Cosmos.DataTransfer.AzureTableAPIExtension.Settings;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.AzureTableAPIExtension
{
    [Export(typeof(IDataSourceExtension))]
    public class AzureTableAPIDataSourceExtension : IDataSourceExtensionWithSettings
    {
        public string DisplayName => "AzureTableAPI";

        public async IAsyncEnumerable<IDataItem> ReadAsync(IConfiguration config, ILogger logger, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var settings = config.Get<AzureTableAPIDataSourceSettings>();
            settings.Validate();

            TableServiceClient serviceClient;

            if (settings!.UseRbacAuth)
            {
                logger.LogInformation("Connecting to Storage account {AccountEndpoint} using {UseRbacAuth} with {EnableInteractiveCredentials}'", settings.AccountEndpoint, nameof(AzureTableAPIDataSourceSettings.UseRbacAuth), nameof(AzureTableAPIDataSourceSettings.EnableInteractiveCredentials));

                var credential = new DefaultAzureCredential(includeInteractiveCredentials: settings.EnableInteractiveCredentials);
#pragma warning disable CS8604 // Validate above ensures AccountEndpoint is not null
                var baseUri = new Uri(settings.AccountEndpoint);
#pragma warning restore CS8604 // Restore warning

                serviceClient = new TableServiceClient(baseUri, credential);
            }
            else
            {
                logger.LogInformation("Connecting to Storage account using {ConnectionString}'", nameof(AzureTableAPIDataSinkSettings.ConnectionString));

                serviceClient = new TableServiceClient(settings.ConnectionString);
            }
            var tableClient = serviceClient.GetTableClient(settings.Table);

            logger.LogInformation("Reading from table '{Table}'", settings.Table);

            AsyncPageable<TableEntity> queryResults;
            if (!string.IsNullOrWhiteSpace(settings.QueryFilter)) {
                logger.LogInformation("Applying QueryFilter: {QueryFilter}", settings.QueryFilter);

                if (settings.QueryFilter.Contains("Timestamp", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning("QueryFilter references the system 'Timestamp' property. " +
                        "Note: Cosmos DB Table API does not support filtering on the system Timestamp property — " +
                        "queries will silently return 0 results. Consider using a custom datetime property instead. " +
                        "This limitation does not apply to Azure Storage Tables.");
                }

                queryResults = tableClient.QueryAsync<TableEntity>(filter: settings.QueryFilter);
            } else {
                logger.LogInformation("No QueryFilter specified, reading all entities");
                queryResults = tableClient.QueryAsync<TableEntity>();
            }

            int itemCount = 0;
            await foreach (var entity in queryResults.WithCancellation(cancellationToken))
            {
                yield return new AzureTableAPIDataItem(entity, settings.PartitionKeyFieldName, settings.RowKeyFieldName);
                itemCount++;
            }

            if (itemCount > 0)
            {
                logger.LogInformation("Read {ItemCount} items from table '{Table}'", itemCount, settings.Table);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(settings.QueryFilter))
                {
                    logger.LogWarning("No items read from table '{Table}' with QueryFilter: {QueryFilter}. Verify the filter syntax is correct for your table API provider.", settings.Table, settings.QueryFilter);
                }
                else
                {
                    logger.LogWarning("No items read from table '{Table}'", settings.Table);
                }
            }
        }

        public IEnumerable<IDataExtensionSettings> GetSettings()
        {
            yield return new AzureTableAPIDataSourceSettings();
        }
    }
}