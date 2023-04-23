using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.JsonExtension.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.JsonExtension
{
    [Export(typeof(IDataSourceExtension))]
    public class JsonDataSourceExtension : IDataSourceExtension
    {
        public string DisplayName => "JSON";
        public async IAsyncEnumerable<IDataItem> ReadAsync(IConfiguration config, ILogger logger, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var settings = config.Get<JsonSourceSettings>();
            settings.Validate();

            if (settings.FilePath != null)
            {
                if (File.Exists(settings.FilePath))
                {
                    logger.LogInformation("Reading file '{FilePath}'", settings.FilePath);
                    var list = ReadFileAsync(settings.FilePath, logger, cancellationToken);

                    if (list != null)
                    {
                        await foreach (var listItem in list.WithCancellation(cancellationToken))
                        {
                            if (listItem != null)
                            {
                                yield return new JsonDictionaryDataItem(listItem);
                            }
                        }
                    }
                }
                else if (Directory.Exists(settings.FilePath))
                {
                    string[] files = Directory.GetFiles(settings.FilePath, "*.json", SearchOption.AllDirectories);
                    logger.LogInformation("Reading {FileCount} files from '{Folder}'", files.Length, settings.FilePath);
                    foreach (string filePath in files.OrderBy(f => f))
                    {
                        logger.LogInformation("Reading file '{FilePath}'", filePath);
                        var list = ReadFileAsync(filePath, logger, cancellationToken);

                        if (list != null)
                        {
                            await foreach (var listItem in list.WithCancellation(cancellationToken))
                            {
                                if (listItem != null)
                                {
                                    yield return new JsonDictionaryDataItem(listItem);
                                }
                            }
                        }
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

                    var list = ReadJsonItemsAsync(json, logger, cancellationToken);

                    if (list != null)
                    {
                        await foreach (var listItem in list.WithCancellation(cancellationToken))
                        {
                            if (listItem != null)
                            {
                                yield return new JsonDictionaryDataItem(listItem);
                            }
                        }
                    }
                }
                else
                {
                    logger.LogWarning("No content was found at configured path '{FilePath}'", settings.FilePath);
                    yield break;
                }

                logger.LogInformation("Completed reading '{FilePath}'", settings.FilePath);
            }
        }

        private static IAsyncEnumerable<Dictionary<string, object?>?>? ReadFileAsync(string filePath, ILogger logger, CancellationToken cancellationToken)
        {
            var jsonFile = File.OpenRead(filePath);
            return ReadJsonItemsAsync(jsonFile, logger, cancellationToken);
        }

        private static IAsyncEnumerable<Dictionary<string, object?>?>? ReadJsonItemsAsync(Stream jsonStream, ILogger logger, CancellationToken cancellationToken)
        {
            if (jsonStream is { CanSeek: true, Length: < 10485760L })
            {
                // test for single item in JSON
                var singleItemList = ReadSingleItemAsync(jsonStream, logger);
                if (singleItemList != null)
                {
                    return singleItemList.ToAsyncEnumerable();
                }
            }

            try
            {
                jsonStream.Seek(0, SeekOrigin.Begin);
                return JsonSerializer.DeserializeAsyncEnumerable<Dictionary<string, object?>>(jsonStream, cancellationToken: cancellationToken);
            }
            catch (Exception)
            {
                // list failed
            }

            return null;
        }

        private static IEnumerable<Dictionary<string, object?>?>? ReadSingleItemAsync(Stream stream, ILogger logger)
        {
            Dictionary<string, object?>? item;
            try
            {
                item = JsonSerializer.Deserialize<Dictionary<string, object?>>(stream);
            }
            catch (Exception)
            {
                // single item failed
                return null;
            }

            if (item != null)
            {
                return new[] { item };
            }

            string textContent;
            try
            {
                var chars = new char[50];
                new StreamReader(stream).ReadBlock(chars, 0, chars.Length);
                textContent = new string(chars);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read stream");
                textContent = "<error>";
            }
            logger.LogWarning("No records read from '{Content}'", textContent);

            return null;
        }
    }
}