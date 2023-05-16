using System.ComponentModel.DataAnnotations;
using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Interfaces.Manifest;

namespace Cosmos.DataTransfer.SqlServerExtension
{
    public class SqlServerSinkSettings : IDataExtensionSettings
    {
        [Required]
        [SensitiveValue]
        public string? ConnectionString { get; set; }
        [Required]
        public string? TableName { get; set; }

        [Range(1, int.MaxValue)]
        public int BatchSize { get; set; } = 1000;

        [MinLength(1)]
        public List<ColumnMapping> ColumnMappings { get; set; } = new List<ColumnMapping>();

    }
}