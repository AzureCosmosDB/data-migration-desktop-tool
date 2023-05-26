namespace Cosmos.DataTransfer.Interfaces.Manifest;

public record ExtensionManifestItem(string Name, ExtensionDirection Direction, string? Version, string? AssemblyName, IEnumerable<ExtensionSettingProperty> Settings)
{

}