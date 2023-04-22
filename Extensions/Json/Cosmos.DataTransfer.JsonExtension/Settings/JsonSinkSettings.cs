using System.ComponentModel.DataAnnotations;
using Cosmos.DataTransfer.Interfaces;

namespace Cosmos.DataTransfer.JsonExtension.Settings
{
    public class JsonSinkSettings : IDataExtensionSettings
    {
        [Required]
        public string? FilePath { get; set; }

        public bool IncludeNullFields { get; set; }
        public bool Indented { get; set; }
        public bool UploadToS3 { get; set; }
        public string S3Region { get; set; }
        public string S3BucketName { get; set; }
        public string S3AccessKey { get; set; }
        public string S3SecretKey { get; set; }
    }
}