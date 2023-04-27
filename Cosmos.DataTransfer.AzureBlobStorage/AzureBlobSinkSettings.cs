using Cosmos.DataTransfer.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace Cosmos.DataTransfer.AzureBlobStorage
{
    public class AzureBlobSinkSettings : IDataExtensionSettings
    {
        [Required]
        public string AzureBlobConnectionString { get; set; } = null!;

        [Required]
        public string AzureBlobContainerName { get; set; } = null!;

        public int? AzureBlobMaxBlockSizeinKB { get; set; }
    }
}