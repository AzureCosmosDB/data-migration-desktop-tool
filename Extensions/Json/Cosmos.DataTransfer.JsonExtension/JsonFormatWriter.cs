using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.JsonExtension.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Cosmos.DataTransfer.JsonExtension;

public class JsonFormatWriter : IFormattedDataWriter
{
    public async Task FormatDataAsync(IAsyncEnumerable<IDataItem> dataItems, Stream target, IConfiguration config, ILogger logger, CancellationToken cancellationToken = default)
    {
        var settings = config.Get<JsonFormatWriterSettings>();
        settings.Validate();

        await using var writer = new Utf8JsonWriter(target, new JsonWriterOptions
        {
            Indented = settings.Indented
        });
        writer.WriteStartArray();

        await foreach (var item in dataItems.WithCancellation(cancellationToken))
        {
            DataItemJsonConverter.WriteDataItem(writer, item, settings.IncludeNullFields);
            int max = settings.BufferSizeMB * 1024 * 1024;
            if (writer.BytesPending > max)
            {
                await writer.FlushAsync(cancellationToken);
            }
        }

        writer.WriteEndArray();
    }

    public IEnumerable<IDataExtensionSettings> GetSettings()
    {
        yield return new JsonFormatWriterSettings();
    }
}