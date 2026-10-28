using System.IO.Compression;
using System.Runtime.CompilerServices;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.Common;

public class FileDataSource : IComposableDataSource
{
    internal TimeSpan HttpReadTimeout { get; init; } = TimeSpan.FromSeconds(100);

    internal Stream? ReadFile(string filePath, CompressionEnum compression, ILogger logger) {
        logger.LogInformation("Reading file '{FilePath}'", filePath);
        var fileStream = File.OpenRead(filePath);
        Stream decompressor;
        if (compression == CompressionEnum.Gzip || 
            compression == CompressionEnum.None && filePath.EndsWith(".gz") ||
            compression == CompressionEnum.None && filePath.EndsWith(".gzip")) {
            decompressor = new GZipStream(fileStream, CompressionMode.Decompress);
        } else if (compression == CompressionEnum.Brotli || 
            compression == CompressionEnum.None && filePath.EndsWith(".br")) {
            decompressor = new BrotliStream(fileStream, CompressionMode.Decompress);
        } else if (compression == CompressionEnum.Deflate || 
            compression == CompressionEnum.None && filePath.EndsWith(".zz")) {
            decompressor = new DeflateStream(fileStream, CompressionMode.Decompress);
        } else {
            decompressor = fileStream;
        }
        return decompressor;
    }

    public async IAsyncEnumerable<Stream?> ReadSourceAsync(IConfiguration config, ILogger logger, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var settings = config.Get<FileSourceSettings>();
        settings.Validate();

        cancellationToken.ThrowIfCancellationRequested();
        var location = settings!.FilePath!;
        if (Uri.TryCreate(location, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            // Query strings and user information can contain credentials.
            location = new UriBuilder(uri) { UserName = "", Password = "", Query = "", Fragment = "" }.Uri.AbsoluteUri;
            logger.LogInformation("Reading from URI '{FilePath}'", location);
            using var handler = new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.All };
            using var client = new HttpClient(handler);
            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                throw new HttpRequestException($"Failed to read HTTP source '{location}'.", ex, ex.StatusCode);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"Failed to read HTTP source '{location}': {(int)response.StatusCode} ({response.StatusCode}).",
                        null, response.StatusCode);
                }

                var body = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var stream = new HttpReadTimeoutStream(body, HttpReadTimeout, location, cancellationToken);
                yield return stream;
            }
        }
        else if (Directory.Exists(settings.FilePath))
        {
            string[] patterns = new string[] { "*.json", "*.json.gz", "*.json.gzip", "*.json.br", "*.json.zz" };
            var files = patterns.SelectMany(pattern =>  
                Directory.GetFiles(settings.FilePath, pattern, SearchOption.AllDirectories));
            logger.LogInformation("Reading {FileCount} files from '{Folder}'", files.Count(), settings.FilePath);
            foreach (string filePath in files.OrderBy(f => f))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return ReadFile(filePath, settings.Compression, logger);
            }
        }
        else
        {
            // Open directly so missing, invalid, and inaccessible paths remain failures.
            yield return ReadFile(settings.FilePath!, settings.Compression, logger);
        }

        logger.LogInformation("Completed reading '{FilePath}'", location);
    }

    public IEnumerable<IDataExtensionSettings> GetSettings()
    {
        yield return new FileSourceSettings();
    }

    private sealed class HttpReadTimeoutStream(Stream source, TimeSpan timeout, string location, CancellationToken sourceCancellation) : Stream
    {
        public override bool CanRead => source.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        // Synchronous consumers need the same deadline; the asynchronous read below
        // does not capture their synchronization context.
        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);
            return ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBufferArguments(buffer, offset, count);
            return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(sourceCancellation, cancellationToken);
            deadline.CancelAfter(timeout);
            try
            {
                return await source.ReadAsync(buffer, deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (deadline.IsCancellationRequested &&
                !sourceCancellation.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new HttpRequestException(
                    $"Timed out reading HTTP source '{location}': no data was received within {timeout.TotalSeconds} seconds.", ex);
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