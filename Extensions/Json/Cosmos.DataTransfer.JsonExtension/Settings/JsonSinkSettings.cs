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
    }
}