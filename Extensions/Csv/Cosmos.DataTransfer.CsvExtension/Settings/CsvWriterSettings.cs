using System.Globalization;
using Cosmos.DataTransfer.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace Cosmos.DataTransfer.CsvExtension.Settings;

public class CsvWriterSettings : IDataExtensionSettings, IValidatableObject
{
    public bool IncludeHeader { get; set; } = true;
    public string? Delimiter { get; set; } = ",";
    public string? Culture { get; set; } = "InvariantCulture";
    public int ItemProgressFrequency { get; set; } = 1000;
    
    public CultureInfo GetCultureInfo() {
        switch (this.Culture?.ToLower())
        {
            case "invariant":
            case "invariantculture": 
                return CultureInfo.InvariantCulture;
            case "current": 
            case "currentculture":
                return CultureInfo.CurrentCulture;
            case "":
            case null:
                throw new ArgumentNullException();
            default: return CultureInfo.GetCultureInfo(this.Culture!);
        }
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ValidationResult? result = null; 
        try {
            _ = this.GetCultureInfo();
        } catch (CultureNotFoundException) {
            result = new ValidationResult(
                $"Could not find CultureInfo `{this.Culture}` on this system.",
                new string[] { "Culture" }
            );
        } catch (ArgumentNullException) {
            result = new ValidationResult(
                $"Culture missing.",
                new string[] { "Culture" }
            );
        }


        if (result != null) {
            yield return result;
        }
    }
}
