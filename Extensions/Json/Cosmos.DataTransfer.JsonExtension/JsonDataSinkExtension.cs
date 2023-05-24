using System.ComponentModel.Composition;
using System.Text.Json;
using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.JsonExtension.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.JsonExtension
{
    [Export(typeof(IDataSinkExtension))]
    public class JsonDataSinkExtension : IDataSinkExtensionWithSettings
    {
        public string DisplayName => "JSON";

        public async Task WriteAsync(IAsyncEnumerable<IDataItem> dataItems, IConfiguration config, IDataSourceExtension dataSource, ILogger logger, CancellationToken cancellationToken = default)
        {
            var settings = config.Get<JsonSinkSettings>();
            settings.Validate();

            if (settings.FilePath != null)
            {
                logger.LogInformation("Writing to file '{FilePath}'", settings.FilePath);
                await SaveFile(dataItems, settings, cancellationToken);
                
                logger.LogInformation("Completed writing data to file '{FilePath}'", settings.FilePath);
            }
        }

        private async Task SaveFile(IAsyncEnumerable<IDataItem> dataItems, JsonSinkSettings settings, CancellationToken cancellationToken = default)
        {
            await using var stream = File.Create(settings.FilePath);
            await using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                Indented = settings.Indented
            });
            writer.WriteStartArray();

            await foreach (var item in dataItems.WithCancellation(cancellationToken))
            {
                DataItemJsonConverter.WriteDataItem(writer, item, settings.IncludeNullFields);
            }

            writer.WriteEndArray();
        }

        public IEnumerable<IDataExtensionSettings> GetSettings()
        {
            yield return new JsonSinkSettings();
        }
    }
}