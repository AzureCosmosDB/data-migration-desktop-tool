using System.ComponentModel.DataAnnotations;
using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Interfaces.Manifest;

namespace Cosmos.DataTransfer.PostgresqlExtension.Settings
{
    public class PostgreBaseSettings : IDataExtensionSettings
    {
        [Required]
        [SensitiveValue]
        public string? ConnectionString { get; set; }
    }
}