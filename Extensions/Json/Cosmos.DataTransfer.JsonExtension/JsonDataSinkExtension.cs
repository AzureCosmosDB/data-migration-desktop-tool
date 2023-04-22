using System.ComponentModel.Composition;
using System.Text.Json;
using System.Threading;
using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.JsonExtension.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.JsonExtension
{
    [Export(typeof(IDataSinkExtension))]
    public class JsonDataSinkExtension : IDataSinkExtension
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
                if (settings.UploadToS3 == true)
                {
                    if (settings.S3Region != null && settings.S3BucketName != null && settings.S3AccessKey != null && settings.S3SecretKey != null)
                    {
                        logger.LogInformation("Saving file to AWS S3 Bucket '{BucketName}'", settings.S3BucketName);
                        await SaveToS3(settings, cancellationToken);
                    }
                    else
                    {
                        logger.LogError("S3 Requires S3Region, S3BucketName, S3AccessKey, and S3SecretKey to be set.");
                    }
                }
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
        private async Task SaveToS3(JsonSinkSettings settings, CancellationToken cancellationToken)
        {
            S3Writer.InitializeS3Client(settings.S3AccessKey, settings.S3SecretKey, settings.S3Region);
            await S3Writer.WriteToS3(settings.S3BucketName, settings.FilePath, cancellationToken);
        }
    }
}