using System.ComponentModel.DataAnnotations;
using Cosmos.DataTransfer.Interfaces;

namespace Cosmos.DataTransfer.SqlServerExtension
{
    public class SqlServerSourceSettings : IDataExtensionSettings
    {
        [Required]
        public string? ConnectionString { get; set; }

        [Required]
        public string? QueryText { get; set; }

    }
}