using System.Runtime.CompilerServices;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Cosmos.DataTransfer.JsonExtension;

public class JsonFormatReader : IFormattedDataReader
{
    public async IAsyncEnumerable<IDataItem> ParseDataAsync(IComposableDataSource sourceExtension, IConfiguration config, ILogger logger, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var streams = sourceExtension.ReadSourceAsync(config, logger, cancellationToken);

        await foreach (var stream in streams.WithCancellation(cancellationToken))
        {
            if (stream != null)
            {
                var list = ReadJsonItemsAsync(stream, logger, cancellationToken);
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
    }

    private static IAsyncEnumerable<Dictionary<string, object?>?>? ReadJsonItemsAsync(Stream jsonStream, ILogger logger, CancellationToken cancellationToken)
    {
        if (jsonStream is { CanSeek: true, Length: < 1485760L })
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
            if (jsonStream is { CanSeek: true })
            {
                jsonStream.Seek(0, SeekOrigin.Begin);
            }
            return JsonSerializer.DeserializeAsyncEnumerable<Dictionary<string, object?>>(jsonStream, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            // list failed
            logger.LogError(ex, "Failed to read JSON list from '{Content}'", ReadTextForLogging(jsonStream, logger));
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

        logger.LogWarning("No records read from '{Content}'", ReadTextForLogging(stream, logger));

        return null;
    }

    private static string ReadTextForLogging(Stream stream, ILogger logger)
    {
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

        return textContent;
    }

    public IEnumerable<IDataExtensionSettings> GetSettings()
    {
        yield break;
    }
}