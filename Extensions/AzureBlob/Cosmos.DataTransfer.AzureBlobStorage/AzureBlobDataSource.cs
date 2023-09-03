using System.Runtime.CompilerServices;
using Azure.Storage.Blobs;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Blobs.Models;

namespace Cosmos.DataTransfer.AzureBlobStorage;

public class AzureBlobDataSource : IComposableDataSource
{
    public async IAsyncEnumerable<Stream?> ReadSourceAsync(IConfiguration config, ILogger logger, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var settings = config.Get<AzureBlobSourceSettings>();
        settings.Validate();

        logger.LogInformation("Reading file '{File}' from Azure Blob Container '{ContainerName}'", settings.BlobName, settings.ContainerName);

        var account = new BlobContainerClient(settings.ConnectionString, settings.ContainerName);
        var blob = account.GetBlockBlobClient(settings.BlobName);
        var existsResponse = await blob.ExistsAsync(cancellationToken: cancellationToken);
        if (!existsResponse)
            yield break;

        var readStream = await blob.OpenReadAsync(new BlobOpenReadOptions(true)
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