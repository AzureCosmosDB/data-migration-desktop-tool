using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Common;
using Cosmos.DataTransfer.JsonExtension.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Cosmos.DataTransfer.JsonExtension;

public class JsonFormatWriter : IFormattedDataWriter
{
    public async Task FormatDataAsync(IAsyncEnumerable<IDataItem> dataItems, Stream target, IConfiguration config, ILogger logger, CancellationToken cancellationToken = default)
    {
        var settings = config.Get<JsonFormatWriterSettings>() ?? new JsonFormatWriterSettings();
        settings.Validate();

        await using var writer = new Utf8JsonWriter(target, new JsonWriterOptions
        {
            Indented = settings.Indented
        });
        writer.WriteStartArray();

        int documentCount = 0;
        await foreach (var item in dataItems.WithCancellation(cancellationToken))
        {
            DataItemJsonConverter.WriteDataItem(writer, item, settings.IncludeNullFields);
            documentCount++;
            
            // Log progress periodically based on DocumentProgressFrequency setting
            if (documentCount % settings.DocumentProgressFrequency == 0)
            {
                logger.LogInformation("Processed {DocumentCount} documents for transfer to Azure Blob", documentCount);
            }
            
            int max = settings.BufferSizeMB * 1024 * 1024;
            if (writer.BytesPending > max)
            {
                await writer.FlushAsync(cancellationToken);
            }
        }

        writer.WriteEndArray();
        
        // Log final document count
        if (documentCount > 0)
            logger.LogInformation("Completed processing {DocumentCount} total documents for transfer to Azure Blob", documentCount);
        else
            logger.LogWarning("No documents were processed for transfer to Azure Blob");
    }

    public IEnumerable<IDataExtensionSettings> GetSettings()
    {
        yield return new JsonFormatWriterSettings();
    }
}