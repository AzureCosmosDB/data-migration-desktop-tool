using Cosmos.DataTransfer.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace Cosmos.DataTransfer.ParqExtension.Settings
{
    public class ParquetSinkSettings : IDataExtensionSettings
    {
        [Required]
        public string? FilePath { get; set; }
        public bool UploadToS3 { get; set; }
        public string S3Region { get; set; }
        public string S3BucketName { get; set; }
        public string S3AccessKey { get; set; }
        public string S3SecretKey { get; set; }

        // Add option to set a custom row group size for very large files.
        //public int? CustomRowGroupSize { get; set; }
    }
}