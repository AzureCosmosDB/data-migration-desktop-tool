using Azure.Storage.Blobs;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Blobs.Models;

namespace Cosmos.DataTransfer.AzureBlobStorage
{
    public class AzureBlobDataSink : IComposableDataSink
    {
        public async Task WriteToTargetAsync(Func<Stream, Task> writeToStream, IConfiguration config, IDataSourceExtension dataSource, ILogger logger, CancellationToken cancellationToken = default)
        {
            var settings = config.Get<AzureBlobSinkSettings>();
            settings.Validate();

            logger.LogInformation("Saving file '{File}' to Azure Blob Container '{ContainerName}'", settings.BlobName, settings.ContainerName);

            var account = new BlobContainerClient(settings.ConnectionString, settings.ContainerName);
            await account.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            var blob = account.GetBlockBlobClient(settings.BlobName);

            await using var blobStream = await blob.OpenWriteAsync(true, new BlockBlobOpenWriteOptions
            {
                BufferSize = settings.MaxBlockSizeinKB * 1024L,
                ProgressHandler = new Progress<long>(l =>
                {
                    logger.LogInformation("Transferred {UploadedBytes} bytes to Azure Blob", l);
                })
            }, cancellationToken);
            await writeToStream(blobStream);
        }

        public IEnumerable<IDataExtensionSettings> GetSettings()
        {
            yield return new AzureBlobSinkSettings();
        }
    }
}