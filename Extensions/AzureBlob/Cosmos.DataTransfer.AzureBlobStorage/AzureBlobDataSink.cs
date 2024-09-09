using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
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
        }

        public IEnumerable<IDataExtensionSettings> GetSettings()
        {
            yield return new AzureBlobSinkSettings();
        }
    }
}