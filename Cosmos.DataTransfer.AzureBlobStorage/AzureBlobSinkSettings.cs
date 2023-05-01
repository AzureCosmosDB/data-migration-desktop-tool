using Cosmos.DataTransfer.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace Cosmos.DataTransfer.AzureBlobStorage
{
    public class AzureBlobSinkSettings : IDataExtensionSettings
    {
        [Required]
        public string ConnectionString { get; set; } = null!;

        [Required]
        public string ContainerName { get; set; } = null!;

        [Required]
        public string BlobName { get; set; } = null!;

        public int? MaxBlockSizeinKB { get; set; }
    }
}