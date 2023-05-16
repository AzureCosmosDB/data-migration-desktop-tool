using Cosmos.DataTransfer.Interfaces;
using System.ComponentModel.DataAnnotations;
using Cosmos.DataTransfer.Interfaces.Manifest;

namespace Cosmos.DataTransfer.AzureBlobStorage
{
    public class AzureBlobSinkSettings : IDataExtensionSettings
    {
        [Required]
        [SensitiveValue]
        public string ConnectionString { get; set; } = null!;

        [Required]
        public string ContainerName { get; set; } = null!;

        [Required]
        public string BlobName { get; set; } = null!;

        public int? MaxBlockSizeinKB { get; set; }
    }
}