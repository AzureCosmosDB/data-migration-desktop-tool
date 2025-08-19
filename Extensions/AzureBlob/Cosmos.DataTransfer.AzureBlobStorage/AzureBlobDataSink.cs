using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Common;
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

            BlobContainerClient account;
            if (settings.UseRbacAuth)
            {
                logger.LogInformation("Connecting to Storage account {AccountEndpoint} using {UseRbacAuth} with {EnableInteractiveCredentials}'", settings.AccountEndpoint, nameof(AzureBlobSourceSettings.UseRbacAuth), nameof(AzureBlobSourceSettings.EnableInteractiveCredentials));

                var credential = new DefaultAzureCredential(includeInteractiveCredentials: settings.EnableInteractiveCredentials);
#pragma warning disable CS8604 // Validate above ensures AccountEndpoint is not null
                var baseUri = new Uri(settings.AccountEndpoint);
                var blobContainerUri = new Uri(baseUri, settings.ContainerName);
#pragma warning restore CS8604 // Restore warning

                account = new BlobContainerClient(blobContainerUri, credential);
            }
            else
            {
                logger.LogInformation("Connecting to Storage account using {ConnectionString}'", nameof(AzureBlobSourceSettings.ConnectionString));

                account = new BlobContainerClient(settings.ConnectionString, settings.ContainerName);
            }

            await account.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            var blob = account.GetBlockBlobClient(settings.BlobName);

            logger.LogInformation("Saving file '{File}' to Azure Blob Container '{ContainerName}'", settings.BlobName, settings.ContainerName);

            await using var blobStream = await blob.OpenWriteAsync(true, new BlockBlobOpenWriteOptions
            {
                BufferSize = settings.MaxBlockSizeinKB * 1024L,
                ProgressHandler = new Progress<long>(l =>
                {
                    logger.LogInformation("Transferred {UploadedBytes} bytes to Azure Blob", l);
                })
            }, cancellationToken);
            await writeToStream(blobStream);
            
            // Log final summary after upload completes
            var finalBlob = account.GetBlobClient(settings.BlobName);
            var properties = await finalBlob.GetPropertiesAsync(cancellationToken: cancellationToken);
            
            // Get the item count from the format writer
            var itemCount = ItemProgressTracker.ItemCount;
            
            if (itemCount > 0)
            {
                logger.LogInformation("Successfully transferred {TotalBytes} total bytes from {ItemCount} items to blob '{BlobName}' in container '{ContainerName}'", 
                    properties.Value.ContentLength, itemCount, settings.BlobName, settings.ContainerName);
            }
            else
            {
                logger.LogInformation("Successfully transferred {TotalBytes} total bytes to blob '{BlobName}' in container '{ContainerName}'", 
                    properties.Value.ContentLength, settings.BlobName, settings.ContainerName);
            }
        }

        public IEnumerable<IDataExtensionSettings> GetSettings()
        {
            yield return new AzureBlobSinkSettings();
        }
    }
}