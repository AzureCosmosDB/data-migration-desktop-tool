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

        /// <summary>
        /// Specifies the behavior when writing data to SQL Server.
        /// Insert: Inserts new records only (default).
        /// Upsert: Uses SQL MERGE to insert or update based on primary key columns.
        /// </summary>
        public SqlWriteMode WriteMode { get; set; } = SqlWriteMode.Insert;

        /// <summary>
        /// List of column names that form the primary key for the table.
        /// Required when WriteMode is Upsert. These columns are used in the MERGE ON clause.
        /// </summary>
        public List<string> PrimaryKeyColumns { get; set; } = new List<string>();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();
            
            // Standard validation
            Validator.TryValidateObject(this, validationContext, results, true);

            // Custom validation for Upsert mode
            if (WriteMode == SqlWriteMode.Upsert && (PrimaryKeyColumns == null || PrimaryKeyColumns.Count == 0))
            {
                results.Add(new ValidationResult(
                    "PrimaryKeyColumns must be specified when WriteMode is Upsert.",
                    new[] { nameof(PrimaryKeyColumns) }));
            }

            return results;
        }
    }
}