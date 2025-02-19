using System.ComponentModel.DataAnnotations;
using Cosmos.DataTransfer.Interfaces;

namespace Cosmos.DataTransfer.Common;

public class FileSinkSettings : IDataExtensionSettings, IValidatableObject
{
    [Required]
    public string? FilePath { get; set; }
    public bool Append { get; set; } = false;
    public CompressionEnum Compression { get; set; } = CompressionEnum.None;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (this.Append && Compression != CompressionEnum.None) {
            // Technically we can, but the .NET methods here
            // cannot read the concatenated, compressed files.
            yield return new ValidationResult(
                "Cannot Append to any compressed files.",
                new string[] { "Append", "Compression" }
            );
        }
    }
}