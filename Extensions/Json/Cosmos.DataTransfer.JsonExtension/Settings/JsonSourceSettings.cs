using System.ComponentModel.DataAnnotations;
using Cosmos.DataTransfer.Interfaces;

namespace Cosmos.DataTransfer.JsonExtension.Settings
{
    public class JsonSourceSettings : IDataExtensionSettings
    {
        [Required]
        public string? FilePath { get; set; }
    }
}