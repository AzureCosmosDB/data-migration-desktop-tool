using System.Runtime.CompilerServices;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.AwsS3Storage;

public class AwsS3DataSource : IComposableDataSource
{
    public async IAsyncEnumerable<Stream?> ReadSourceAsync(IConfiguration config, ILogger logger, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var settings = config.Get<AwsS3SourceSettings>();
        settings.Validate();

        logger.LogInformation("Reading file {File} from AWS S3 Bucket '{BucketName}'", settings.FileName, settings.S3BucketName);

        using var s3 = new S3Client(settings.S3AccessKey, settings.S3SecretKey, settings.S3Region);
        var stream = await s3.ReadFromS3(settings.S3BucketName, settings.FileName, cancellationToken);
        yield return stream;
    }

    public IEnumerable<IDataExtensionSettings> GetSettings()
    {
        yield return new AwsS3SourceSettings();
    }
}