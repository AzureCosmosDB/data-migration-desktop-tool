using System.ComponentModel.DataAnnotations;

namespace Cosmos.DataTransfer.SqlServerExtension
{
    public class ColumnMapping
    {
        [Required]
        public string? ColumnName { get; set; }
        public string? SourceFieldName { get; set; }
        public bool AllowNull { get; set; } = true;
        public object? DefaultValue { get; set; }
        public string? DataType { get; set; }

        public string? GetFieldName()
        {
            return !string.IsNullOrEmpty(SourceFieldName) ? SourceFieldName : ColumnName;
        }
    }
}