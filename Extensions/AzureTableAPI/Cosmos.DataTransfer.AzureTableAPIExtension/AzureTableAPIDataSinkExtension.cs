using System.ComponentModel.Composition;
using Azure.Data.Tables;
using Azure.Identity;
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

            TableServiceClient serviceClient;

            if (settings.UseRbacAuth)
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
                logger.LogInformation("Connecting to Storage account using {ConnectionString}'", nameof(AzureTableAPIDataSinkSettings.ConnectionString));

                serviceClient = new TableServiceClient(settings.ConnectionString);
            }

            var tableClient = serviceClient.GetTableClient(settings.Table);

            await tableClient.CreateIfNotExistsAsync();

            var createTasks = new List<Task>();
            await foreach(var item in dataItems.WithCancellation(cancellationToken))
            {
               var entity = item.ToTableEntity(settings.PartitionKeyFieldName, settings.RowKeyFieldName);
               createTasks.Add(tableClient.AddEntityAsync(entity));
            }

            await Task.WhenAll(createTasks);
        }

        public IEnumerable<IDataExtensionSettings> GetSettings()
        {
            yield return new AzureTableAPIDataSinkSettings();
        }
    }
}
