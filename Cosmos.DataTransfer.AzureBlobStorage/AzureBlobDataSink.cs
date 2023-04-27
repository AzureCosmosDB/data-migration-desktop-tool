using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.AzureBlobStorage
{
    public class AzureBlobDataSink : IComposableDataSink
    {
        public async Task WriteToTargetAsync(IFormattedDataWriter dataWriter, IAsyncEnumerable<IDataItem> dataItems, IConfiguration config, IDataSourceExtension dataSource, ILogger logger, CancellationToken cancellationToken = default)
        {
            var settings = config.Get<AzureBlobSinkSettings>();
            settings.Validate();

            logger.LogInformation("Saving file to Azure Blob Container '{ContainerName}'", settings.AzureBlobContainerName);
            BlobWriter.InitializeAzureBlobClient(settings.AzureBlobConnectionString, settings.AzureBlobContainerName, settings.AzureBlobContainerName);
            await using var stream = new MemoryStream();
            await dataWriter.FormatDataAsync(dataItems, stream, config, logger, cancellationToken);
            await BlobWriter.WriteToAzureBlob(stream.ToArray(), settings.AzureBlobMaxBlockSizeinKB, cancellationToken);
        }
    }
}