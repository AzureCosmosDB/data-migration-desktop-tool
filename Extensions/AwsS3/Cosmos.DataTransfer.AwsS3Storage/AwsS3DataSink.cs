using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.AwsS3Storage
{
    public class AwsS3DataSink : IComposableDataSink
    {
        public async Task WriteToTargetAsync(Func<Stream, Task> writeToStream, IConfiguration config, IDataSourceExtension dataSource, ILogger logger, CancellationToken cancellationToken = default)
        {
            var settings = config.Get<AwsS3SinkSettings>();
            settings.Validate();

            logger.LogInformation("Saving file {File} to AWS S3 Bucket '{BucketName}'", settings.FileName, settings.S3BucketName);

            using var s3 = new S3Client(settings.S3AccessKey, settings.S3SecretKey, settings.S3Region);
            await using var stream = new MemoryStream();
            await writeToStream(stream);
            await s3.WriteToS3(settings.S3BucketName, stream, settings.FileName, cancellationToken);
        }

        public IEnumerable<IDataExtensionSettings> GetSettings()
        {
            yield return new AwsS3SinkSettings();
        }
    }
}