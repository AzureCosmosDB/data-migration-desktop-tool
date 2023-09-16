using System.Runtime.CompilerServices;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.Common;

public class FileDataSource : IComposableDataSource
{
    public async IAsyncEnumerable<Stream?> ReadSourceAsync(IConfiguration config, ILogger logger, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var settings = config.Get<FileSourceSettings>();
        settings.Validate();
        if (settings.FilePath != null)
        {

            if (File.Exists(settings.FilePath))
            {
                logger.LogInformation("Reading file '{FilePath}'", settings.FilePath);
                yield return File.OpenRead(settings.FilePath);
            }
            else if (Directory.Exists(settings.FilePath))
            {
                string[] files = Directory.GetFiles(settings.FilePath, "*.json", SearchOption.AllDirectories);
                logger.LogInformation("Reading {FileCount} files from '{Folder}'", files.Length, settings.FilePath);
                foreach (string filePath in files.OrderBy(f => f))
                {
                    logger.LogInformation("Reading file '{FilePath}'", filePath);
                    yield return File.OpenRead(filePath);
                }
            }
            else if (Uri.IsWellFormedUriString(settings.FilePath, UriKind.RelativeOrAbsolute))
            {
                logger.LogInformation("Reading from URI '{FilePath}'", settings.FilePath);

                HttpClient client = new HttpClient();
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
    }

    public IEnumerable<IDataExtensionSettings> GetSettings()
    {
        yield return new FileSourceSettings();
    }
}