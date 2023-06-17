using Cosmos.DataTransfer.Interfaces;

namespace Cosmos.DataTransfer.CsvExtension.Settings;

public class CsvReaderSettings : IDataExtensionSettings
{
    public bool HasHeader { get; set; } = true;
    public string? ColumnNameFormat { get; set; } = "column_{0}";
    public string Delimiter { get; set; } = ",";
}
