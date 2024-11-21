using System.ComponentModel.Composition;
using Azure.Data.Tables;
using Cosmos.DataTransfer.AzureTableAPIExtension.Data;
using Cosmos.DataTransfer.AzureTableAPIExtension.Settings;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.AzureTableAPIExtension
{
    [Export(typeof(IDataSinkExtension))]
    public class AzureTableAPIDataSinkExtension : IDataSinkExtensionWithSettings
    {
        public string DisplayName => "AzureTableAPI";

        public async Task WriteAsync(IAsyncEnumerable<IDataItem> dataItems, IConfiguration config, IDataSourceExtension dataSource, ILogger logger, CancellationToken cancellationToken = default)
        {
            var settings = config.Get<AzureTableAPIDataSinkSettings>();
            settings.Validate();

            var serviceClient = new TableServiceClient(settings.ConnectionString);
            var tableClient = serviceClient.GetTableClient(settings.Table);

            await tableClient.CreateIfNotExistsAsync(cancellationToken);

            await Parallel.ForEachAsync(GetBatches(dataItems, settings), new ParallelOptions() { MaxDegreeOfParallelism = 8 }, async (batch, token) =>
            {
                await InnerWriteAsync(batch, tableClient, logger, token);
            });
        }

        private static async IAsyncEnumerable<List<TableEntity>> GetBatches(IAsyncEnumerable<IDataItem> dataItems, AzureTableAPIDataSinkSettings settings)
        {
            var entities = new List<TableEntity>();
            var first = true;
            var partitionKey = string.Empty;

            await foreach (var item in dataItems)
            {
                var tableEntity = item.ToTableEntity(settings.PartitionKeyFieldName, settings.RowKeyFieldName);

                if (first)
                {
                    partitionKey = tableEntity.PartitionKey;
                    first = false;
                }

                if (!tableEntity.PartitionKey.Equals(partitionKey) || entities.Count == 100)
                {
                    yield return entities;
                    entities = new List<TableEntity>();
                    partitionKey = tableEntity.PartitionKey;
                }

                entities.Add(tableEntity);
            }

            if (entities.Count > 0)
            {
                yield return entities;
            }
        }

        private static async Task InnerWriteAsync(List<TableEntity> tableEntities, TableClient tableClient, ILogger logger, CancellationToken cancellationToken)
        {
            var transactionsActions = tableEntities.Select(e => new TableTransactionAction(TableTransactionActionType.UpsertReplace, e));

            try
            {
                await tableClient.SubmitTransactionAsync(transactionsActions, cancellationToken);
                return;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Batch transaction failed, processing entities one by one instead.");
            }

            foreach (var entity in tableEntities)
            {
                try
                {
                    // Do an upsert here because there could already be some successful entities added from the batch
                    await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Adding a single entity failed, continuing with other entities.");
                }
            }
        }

        public IEnumerable<IDataExtensionSettings> GetSettings()
        {
            yield return new AzureTableAPIDataSinkSettings();
        }
    }
}
