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

            var entities = new List<TableEntity>();

            await foreach (var item in dataItems.WithCancellation(cancellationToken))
            {
                var entity = item.ToTableEntity(settings.PartitionKeyFieldName, settings.RowKeyFieldName);
                entities.Add(entity);

                if (entities.Count == 100)
                {
                    await InnerWriteAsync(entities, tableClient, logger, cancellationToken);
                    entities.Clear();
                }
            }
        }

        private static async Task InnerWriteAsync(List<TableEntity> tableEntities, TableClient tableClient, ILogger logger, CancellationToken cancellationToken)
        {
            var transactionsActions = tableEntities.Select(e => new TableTransactionAction(TableTransactionActionType.Add, e));

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
