using Cosmos.DataTransfer.Interfaces.Manifest;
using System.ComponentModel.DataAnnotations;

namespace Cosmos.DataTransfer.PostgresqlExtension.Settings
{
    public class PostgreSourceSettings:PostgreBaseSettings
    {
        [Required]
        [SensitiveValue]
        public string? ConnectionString { get; set; }

        [Required]
        public string? QueryText { get; set; }
    }
}
