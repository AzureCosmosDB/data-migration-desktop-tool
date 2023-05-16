using System.ComponentModel.DataAnnotations;
using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Interfaces.Manifest;

namespace Cosmos.DataTransfer.SqlServerExtension
{
    public class SqlServerSourceSettings : IDataExtensionSettings
    {
        [Required]
        [SensitiveValue]
        public string? ConnectionString { get; set; }

        [Required]
        public string? QueryText { get; set; }

    }
}