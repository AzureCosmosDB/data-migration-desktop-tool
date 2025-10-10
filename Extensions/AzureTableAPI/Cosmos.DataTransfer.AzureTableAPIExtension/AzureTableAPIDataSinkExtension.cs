using System.ComponentModel.Composition;
using Azure;
using Azure.Data.Tables;
using Azure.Identity;
using Cosmos.DataTransfer.AzureTableAPIExtension.Data;
using Cosmos.DataTransfer.AzureTableAPIExtension.Settings;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;

namespace Cosmos.DataTransfer.AzureTableAPIExtension
{
    [Export(typeof(IDataSinkExtension))]
    public class AzureTableAPIDataSinkExtension : IDataSinkExtensionWithSettings
    {
        private static readonly int[] TransientStatusCodes = { 408, 429, 500, 502, 503, 504 };

        public string DisplayName => "AzureTableAPI";

        public async Task WriteAsync(IAsyncEnumerable<IDataItem> dataItems, IConfiguration config, IDataSourceExtension dataSource, ILogger logger, CancellationToken cancellationToken = default)
        {
            var settings = config.Get<AzureTableAPIDataSinkSettings>();
            settings.Validate();

            TableServiceClient serviceClient;

            if (settings!.UseRbacAuth)
            {
                logger.LogInformation("Connecting to Storage account {AccountEndpoint} using {UseRbacAuth} with {EnableInteractiveCredentials}", settings.AccountEndpoint, nameof(AzureTableAPIDataSinkSettings.UseRbacAuth), nameof(AzureTableAPIDataSinkSettings.EnableInteractiveCredentials));

                var credential = new DefaultAzureCredential(includeInteractiveCredentials: settings.EnableInteractiveCredentials);
#pragma warning disable CS8604 // Validate above ensures AccountEndpoint is not null
                var baseUri = new Uri(settings.AccountEndpoint);
#pragma warning restore CS8604 // Restore warning

                serviceClient = new TableServiceClient(baseUri, credential);
            }
            else
            {
                logger.LogInformation("Connecting to Storage account using {ConnectionString}", nameof(AzureTableAPIDataSinkSettings.ConnectionString));

                serviceClient = new TableServiceClient(settings.ConnectionString);
            }

            var tableClient = serviceClient.GetTableClient(settings.Table);

            await tableClient.CreateIfNotExistsAsync().ConfigureAwait(false);

            var maxConcurrency = settings.MaxConcurrentEntityWrites ?? 10;
            var writeMode = settings.WriteMode ?? EntityWriteMode.Create;

            logger.LogInformation("Writing data to Azure Table Storage with a maximum of {MaxConcurrency} concurrent writes.", maxConcurrency);

            logger.LogInformation("Using PartitionKeyFieldName: `{ParitionKeyFieldName}` and RowKeyFieldName: `{RowKeyFieldName}`", settings.PartitionKeyFieldName, settings.RowKeyFieldName);

            await Parallel.ForEachAsync<IDataItem>(dataItems,
            new ParallelOptions { MaxDegreeOfParallelism = maxConcurrency, CancellationToken = cancellationToken },
            async (item, ct) =>
            {
                try
                {
                    var entity = item.ToTableEntity(settings.PartitionKeyFieldName, settings.RowKeyFieldName);
                    await AddEntityWithRetryAsync(tableClient, entity, writeMode, ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error adding entity to table.");
                }
            });
            logger.LogInformation("Finished writing data to Azure Table Storage.");
        }

        public IEnumerable<IDataExtensionSettings> GetSettings()
        {
            yield return new AzureTableAPIDataSinkSettings();
        }

        /// <summary>
        /// Adds an entity to the Azure Table Storage with retry logic for transient errors.
        /// This method uses the Polly library to implement a retry policy with exponential backoff.
        /// </summary>
        /// <param name="tableClient"></param>
        /// <param name="entity"></param>
        /// <param name="writeMode"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private static async Task AddEntityWithRetryAsync(TableClient tableClient, TableEntity entity, EntityWriteMode writeMode, CancellationToken cancellationToken)
        {
            var retryPolicy = Policy
                .Handle<RequestFailedException>(ex => TransientStatusCodes.Contains(ex.Status))
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

            await retryPolicy.ExecuteAsync(async () =>
            {
                switch (writeMode)
                {
                    case EntityWriteMode.Create:
                        await tableClient.AddEntityAsync(entity, cancellationToken);
                        break;
                    case EntityWriteMode.Replace:
                        await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
                        break;
                    case EntityWriteMode.Merge:
                        await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge, cancellationToken);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(writeMode), writeMode, "Unsupported EntityWriteMode");
                }
            });
        }
    }
}
