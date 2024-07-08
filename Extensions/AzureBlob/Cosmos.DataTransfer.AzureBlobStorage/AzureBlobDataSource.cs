using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Cosmos.DataTransfer.AzureBlobStorage;

public class AzureBlobDataSource : IComposableDataSource
{
    public async IAsyncEnumerable<Stream?> ReadSourceAsync(IConfiguration config, ILogger logger, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var settings = config.Get<AzureBlobSourceSettings>();
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
        
        var blob = account.GetBlockBlobClient(settings.BlobName);
        var existsResponse = await blob.ExistsAsync(cancellationToken: cancellationToken);
        if (!existsResponse)
            yield break;

        logger.LogInformation("Reading file '{File}' from Azure Blob Container '{ContainerName}'", settings.BlobName, settings.ContainerName);

        var readStream = await blob.OpenReadAsync(new BlobOpenReadOptions(false)
        {
            BufferSize = settings.ReadBufferSizeInKB,
        }, cancellationToken: cancellationToken);

        yield return readStream;
    }

    public IEnumerable<IDataExtensionSettings> GetSettings()
    {
        yield return new AzureBlobSourceSettings();
    }
}