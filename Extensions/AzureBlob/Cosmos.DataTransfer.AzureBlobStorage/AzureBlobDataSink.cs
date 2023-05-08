using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.AzureBlobStorage
{
    public class AzureBlobDataSink : IComposableDataSink
    {
        public async Task WriteToTargetAsync(Func<Stream, Task> writeToStream, IConfiguration config, IDataSourceExtension dataSource, ILogger logger, CancellationToken cancellationToken = default)
        {
            var settings = config.Get<AzureBlobSinkSettings>();
            settings.Validate();

            logger.LogInformation("Saving file '{File}' to Azure Blob Container '{ContainerName}'", settings.BlobName, settings.ContainerName);
            BlobWriter.InitializeAzureBlobClient(settings.ConnectionString, settings.ContainerName, settings.BlobName);
            await using var stream = new MemoryStream();
            await writeToStream(stream);
            await BlobWriter.WriteToAzureBlob(stream.ToArray(), settings.MaxBlockSizeinKB, cancellationToken);
        }
    }
}