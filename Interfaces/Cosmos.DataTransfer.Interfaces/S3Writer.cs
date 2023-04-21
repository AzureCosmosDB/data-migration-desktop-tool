using Amazon;
using Amazon.S3;
using Amazon.S3.Transfer;

namespace Cosmos.DataTransfer.Interfaces
{
    public static class S3Writer
    {
        private static IAmazonS3 s3Client;

        public static void InitializeS3Client(string accessKey, string secretKey, string regionname)
        {
            RegionEndpoint region = RegionEndpoint.GetBySystemName(regionname);            
            s3Client = new AmazonS3Client(accessKey, secretKey, region);            
        }

        public static async Task WriteToS3(string bucketName, string filename, CancellationToken cancellationToken)
        {
            var ftu = new TransferUtility(s3Client);
            await ftu.UploadAsync(filename, bucketName, cancellationToken);
        }
    }
}