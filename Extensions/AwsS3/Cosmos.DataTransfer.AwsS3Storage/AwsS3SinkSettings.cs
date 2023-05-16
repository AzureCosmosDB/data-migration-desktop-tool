using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Interfaces.Manifest;
using System.ComponentModel.DataAnnotations;

namespace Cosmos.DataTransfer.AwsS3Storage
{
    public class AwsS3SinkSettings : IDataExtensionSettings
    {
        [Required]
        public string FileName { get; set; } = null!;
        [Required]
        public string S3Region { get; set; } = null!;
        [Required]
        public string S3BucketName { get; set; } = null!;
        [Required]
        [SensitiveValue]
        public string S3AccessKey { get; set; } = null!;
        [Required]
        [SensitiveValue]
        public string S3SecretKey { get; set; } = null!;
    }
}