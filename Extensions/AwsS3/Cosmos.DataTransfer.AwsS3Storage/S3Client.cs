using Amazon;
using Amazon.S3;
using Amazon.S3.Transfer;
using System.Threading;

namespace Cosmos.DataTransfer.AwsS3Storage
{
    public class S3Client : IDisposable
    {
        private readonly IAmazonS3 _s3Client;

        public S3Client(string accessKey, string secretKey, string regionName)
        {
            RegionEndpoint region = RegionEndpoint.GetBySystemName(regionName);            
            _s3Client = new AmazonS3Client(accessKey, secretKey, region);            
        }

        public async Task WriteToS3(string bucketName, Stream data, string filename, CancellationToken cancellationToken)
        {
            var ftu = new TransferUtility(_s3Client);
            await ftu.UploadAsync(data, bucketName, filename, cancellationToken);
        }

        public async Task<Stream> ReadFromS3(string bucketName, string filename, CancellationToken cancellationToken)
        {
            var ftu = new TransferUtility(_s3Client);
            var stream = await ftu.OpenStreamAsync(bucketName, filename, cancellationToken);
            return stream;
        }

        public void Dispose()
        {
            _s3Client.Dispose();
        }
    }
}