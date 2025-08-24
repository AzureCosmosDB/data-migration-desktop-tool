using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Common;
using Cosmos.DataTransfer.JsonExtension.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Cosmos.DataTransfer.JsonExtension;

public class JsonFormatWriter : IFormattedDataWriter, IProgressAwareFormattedDataWriter
{
    public async Task FormatDataAsync(IAsyncEnumerable<IDataItem> dataItems, Stream target, IConfiguration config, ILogger logger, CancellationToken cancellationToken = default)
    {
        // Call the progress-aware version with null progress
        await FormatDataAsync(dataItems, target, config, logger, null, cancellationToken);
    }

    public async Task FormatDataAsync(IAsyncEnumerable<IDataItem> dataItems, Stream target, IConfiguration config, ILogger logger, IProgress<DataTransferProgress>? progress, CancellationToken cancellationToken = default)
    {
        var settings = config.Get<JsonFormatWriterSettings>() ?? new JsonFormatWriterSettings();
        settings.Validate();

        // Track progress locally
        int itemCount = 0;

        await using var writer = new Utf8JsonWriter(target, new JsonWriterOptions
        {
            Indented = settings.Indented
        });
        writer.WriteStartArray();

        await foreach (var item in dataItems.WithCancellation(cancellationToken))
        {
            DataItemJsonConverter.WriteDataItem(writer, item, settings.IncludeNullFields);
            itemCount++;
            
            // Report progress if progress reporter is available
            if (progress != null && itemCount % settings.ItemProgressFrequency == 0)
            {
                progress.Report(new DataTransferProgress(itemCount, 0, $"Formatted {itemCount} items for transfer"));
            }
            
            int max = settings.BufferSizeMB * 1024 * 1024;
            if (writer.BytesPending > max)
            {
                await writer.FlushAsync(cancellationToken);
            }
        }

        writer.WriteEndArray();
        
        // Report final count
        if (progress != null && itemCount > 0)
        {
            progress.Report(new DataTransferProgress(itemCount, 0, null));
        }
    }

    public IEnumerable<IDataExtensionSettings> GetSettings()
    {
        yield return new JsonFormatWriterSettings();
    }
}