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

        if (File.Exists(settings!.FilePath!))
        {
            yield return ReadFile(settings.FilePath, settings.Compression, logger);
        }
        else if (Directory.Exists(settings.FilePath))
        {
            string[] patterns = new string[] { "*.json", "*.json.gz", "*.json.gzip", "*.json.br", "*.json.zz" };
            var files = patterns.SelectMany(pattern =>  
                Directory.GetFiles(settings.FilePath, pattern, SearchOption.AllDirectories));
            logger.LogInformation("Reading {FileCount} files from '{Folder}'", files.Count(), settings.FilePath);
            foreach (string filePath in files.OrderBy(f => f))
            {
                yield return ReadFile(filePath, settings.Compression, logger);
            }
        }
        else if (Uri.IsWellFormedUriString(settings.FilePath, UriKind.Absolute))
        {
            logger.LogInformation("Reading from URI '{FilePath}'", settings.FilePath);
            HttpClientHandler handler = new HttpClientHandler() { AutomaticDecompression = System.Net.DecompressionMethods.All };
            HttpClient client = new HttpClient(handler);
            var response = await client.GetAsync(settings.FilePath, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to read {FilePath}. Response was: {ResponseCode} {ResponseMessage}", settings.FilePath, response.StatusCode, response.ReasonPhrase);
                yield break;
            }

            var json = await response.Content.ReadAsStreamAsync(cancellationToken);

            yield return json;
        }
        else
        {
            logger.LogWarning("No content was found at configured path '{FilePath}'", settings.FilePath);
            yield break;
        }

        logger.LogInformation("Completed reading '{FilePath}'", settings.FilePath);
    }

    public IEnumerable<IDataExtensionSettings> GetSettings()
    {
        yield return new FileSourceSettings();
    }
}