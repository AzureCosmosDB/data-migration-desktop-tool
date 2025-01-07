using System.Globalization;
using Cosmos.DataTransfer.Interfaces;

namespace Cosmos.DataTransfer.CsvExtension.Settings;

public class CsvWriterSettings : IDataExtensionSettings
{
    public bool IncludeHeader { get; set; } = true;
    public string Delimiter { get; set; } = ",";
    public string Culture { get; set; } = "InvariantCulture";
    public CultureInfo GetCultureInfo() {
        switch (this.Culture.ToLower())
        {
            case "invariantculture": return CultureInfo.InvariantCulture;
            case "current": return CultureInfo.CurrentCulture;
            default: return CultureInfo.GetCultureInfo(this.Culture);
        }
    }
}
