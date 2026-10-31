using Azure;
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
    private readonly Func<AzureBlobSourceSettings, BlockBlobClient> createBlobClient;

    public AzureBlobDataSource() : this(CreateBlobClient)
    {
    }

    internal AzureBlobDataSource(Func<AzureBlobSourceSettings, BlockBlobClient> createBlobClient)
    {
        this.createBlobClient = createBlobClient;
    }

    public async IAsyncEnumerable<Stream?> ReadSourceAsync(IConfiguration config, ILogger logger, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var settings = config.Get<AzureBlobSourceSettings>();
        settings.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        var location = $"blob '{SafeLocation(settings!.BlobName)}' in container '{SafeLocation(settings.ContainerName)}'";
        logger.LogInformation("Reading Azure Blob source {Location}", location);

        BlockBlobClient blob;
        bool exists;
        try
        {
            blob = createBlobClient(settings);
            exists = await blob.ExistsAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            throw new IOException($"Invalid Azure Blob source configuration for {location}. Check AccountEndpoint, ConnectionString, ContainerName and BlobName.");
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            throw ReadFailure(ex, location);
        }

        if (!exists)
        {
            throw new FileNotFoundException($"Azure Blob source {location} was not found. Check ContainerName and BlobName and ensure the source blob exists.");
        }

        Stream readStream;
        try
        {
            readStream = await blob.OpenReadAsync(new BlobOpenReadOptions(false)
            {
                BufferSize = settings.ReadBufferSizeInKB,
            }, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            throw ReadFailure(ex, location);
        }

        yield return new BlobReadStream(readStream, location);
    }

    private static BlockBlobClient CreateBlobClient(AzureBlobSourceSettings settings)
    {
        BlobContainerClient account;
        if (settings.UseRbacAuth)
        {
            var credential = new DefaultAzureCredential(includeInteractiveCredentials: settings.EnableInteractiveCredentials);
            var baseUri = new Uri(settings.AccountEndpoint!);
            var blobContainerUri = new Uri(baseUri, settings.ContainerName);
            account = new BlobContainerClient(blobContainerUri, credential);
        }
        else
        {
            account = new BlobContainerClient(settings.ConnectionString, settings.ContainerName);
        }
        return account.GetBlockBlobClient(settings.BlobName);
    }

    private static string SafeLocation(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return new UriBuilder(uri) { UserName = "", Password = "", Query = "", Fragment = "" }.Uri.AbsoluteUri;
        }
        return value.Split('?', '#')[0];
    }

    private static bool IsReadFailure(Exception error) =>
        error is RequestFailedException or IOException or HttpRequestException or AuthenticationFailedException;

    private static IOException ReadFailure(Exception error, string location)
    {
        int? status = error switch
        {
            RequestFailedException request => request.Status,
            HttpRequestException request => (int?)request.StatusCode,
            _ => null
        };
        var detail = status.HasValue ? $" (HTTP {status})" : "";
        // SDK messages, response bodies and inner exceptions may contain signed URLs.
        return new IOException($"Failed to read Azure Blob source {location}{detail}. Check that the blob exists and that the configured credentials have read access.");
    }

    public IEnumerable<IDataExtensionSettings> GetSettings()
    {
        yield return new AzureBlobSourceSettings();
    }

    private sealed class BlobReadStream(Stream source, string location) : Stream
    {
        public override bool CanRead => source.CanRead;
        public override bool CanSeek => source.CanSeek;
        public override bool CanWrite => false;
        public override long Length => source.Length;
        public override long Position { get => source.Position; set => Seek(value, SeekOrigin.Begin); }
        public override void Flush() => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin)
        {
            try
            {
                return source.Seek(offset, origin);
            }
            catch (Exception ex) when (IsReadFailure(ex))
            {
                throw ReadFailure(ex, location);
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            try
            {
                return source.Read(buffer, offset, count);
            }
            catch (Exception ex) when (IsReadFailure(ex))
            {
                throw ReadFailure(ex, location);
            }
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBufferArguments(buffer, offset, count);
            return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            try
            {
                return await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsReadFailure(ex))
            {
                throw ReadFailure(ex, location);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                source.Dispose();
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await source.DisposeAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
    }
}