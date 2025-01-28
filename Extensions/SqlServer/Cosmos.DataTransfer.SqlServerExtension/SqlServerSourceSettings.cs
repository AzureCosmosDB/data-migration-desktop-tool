using System.ComponentModel.DataAnnotations;
using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Interfaces.Manifest;

namespace Cosmos.DataTransfer.SqlServerExtension
{
    public class SqlServerSourceSettings : IDataExtensionSettings, IValidatableObject
    {
        [SensitiveValue]
        public string? ConnectionString { get; set; }

        public string? QueryText { get; set; }

        public string? FilePath { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (String.IsNullOrWhiteSpace(this.ConnectionString)) {
                yield return new ValidationResult("The `ConnectionString` field is required.",
                    new string[] { "ConnectionString" });
            } 
            if (String.IsNullOrWhiteSpace(this.QueryText) &&
                String.IsNullOrWhiteSpace(this.FilePath)) {
                    yield return new ValidationResult(
                        "Either `QueryText` or `FilePath` are required!",
                        new string[] { "QueryText", "FilePath"});
            } else if (String.IsNullOrWhiteSpace(this.QueryText) == false &&
                String.IsNullOrWhiteSpace(this.FilePath) == false) {
                    yield return new ValidationResult(
                        "Both `QueryText` and `FilePath` are not allowed.",
                        new string[] { "QueryText", "FilePath"});
            }
        }
    }
}