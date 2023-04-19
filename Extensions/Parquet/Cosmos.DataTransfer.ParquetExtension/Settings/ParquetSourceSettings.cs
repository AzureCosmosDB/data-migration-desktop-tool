using Cosmos.DataTransfer.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace Cosmos.DataTransfer.ParqExtension.Settings
{
    public class ParquetSourceSettings : IDataExtensionSettings
    {
        [Required]
        public string? FilePath { get; set; }
    }
}