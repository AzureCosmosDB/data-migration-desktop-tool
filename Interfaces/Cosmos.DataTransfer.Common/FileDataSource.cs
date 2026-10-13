using System.IO.Compression;
using System.Runtime.CompilerServices;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.Common;

public class FileDataSource : IComposableDataSource
{
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

                yield return await response.Content.ReadAsStreamAsync(cancellationToken);
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
}