using Cosmos.DataTransfer.Interfaces;

namespace Cosmos.DataTransfer.CsvExtension.Settings;

public class CsvWriterSettings : IDataExtensionSettings
{
    public bool IncludeHeader { get; set; } = true;
    public string Delimiter { get; set; } = ",";
}
