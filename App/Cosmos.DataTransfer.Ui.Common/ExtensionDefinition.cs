namespace Cosmos.DataTransfer.Ui.Common;


public record ExtensionDefinition(string DisplayName);
public record AppExtensions(IEnumerable<ExtensionDefinition> Sources, IEnumerable<ExtensionDefinition> Sinks);

public record ExtensionSettings(ExtensionDefinition Extension, IEnumerable<ExtensionSetting> Settings);

public class MigrationSettings
{
    public string Source { get; set; }
    public string Sink { get; set; }
    public Dictionary<string, object?> SourceSettings { get; set; }
    public Dictionary<string, object?> SinkSettings { get; set; }
}
