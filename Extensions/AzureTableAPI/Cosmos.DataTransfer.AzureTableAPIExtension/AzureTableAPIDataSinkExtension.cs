using System.ComponentModel.Composition;
using Azure.Data.Tables;
using Cosmos.DataTransfer.AzureTableAPIExtension.Data;
using Cosmos.DataTransfer.AzureTableAPIExtension.Settings;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Azure.Cosmos;
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

            await tableClient.CreateIfNotExistsAsync();

            var upsertTasks = new List<Task>();
            string currentDir = Directory.GetCurrentDirectory();
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();

            int totalCount = 0;
            int failedCount = 0;
            int succeedCount = 0;
            using (StreamWriter outputFile = new StreamWriter(Path.Combine(currentDir, "error.log")))
            {
                await foreach(var item in dataItems.WithCancellation(cancellationToken))
                {
                   totalCount++;
                   var entity = item.ToTableEntity(settings.PartitionKeyFieldName, settings.RowKeyFieldName);
                   var task = tableClient.UpsertEntityAsync(entity).ContinueWith(itemResponse =>
                    {
                        if (!itemResponse.IsCompletedSuccessfully)
                        {
                            AggregateException innerExceptions = itemResponse.Exception.Flatten();
                            if (innerExceptions.InnerExceptions.FirstOrDefault(innerEx => innerEx is CosmosException) is CosmosException cosmosException)
                            {
                                throw cosmosException;
                            }
                            else
                            {
                                string entityId = $"Partition key: {entity.PartitionKey}. Row key: {entity.RowKey}.";
                                string message = $"{entityId} Exception {innerExceptions.InnerExceptions.FirstOrDefault()}.";
                                outputFile.WriteLine(message);
                                Console.WriteLine(message);
                                failedCount++;
                            }
                        }
                        else
                        {
                            succeedCount++;
                        }
                    });
                    upsertTasks.Add(task);
                }
            }

            await Task.WhenAll(upsertTasks);

            watch.Stop();
            Console.WriteLine($"Execution Time: {watch.ElapsedMilliseconds} ms");
            Console.WriteLine($"Total entities: {totalCount}. Succeed: {succeedCount}. Failed: {failedCount}.");
        }

        public IEnumerable<IDataExtensionSettings> GetSettings()
        {
            yield return new AzureTableAPIDataSinkSettings();
        }
    }
}
