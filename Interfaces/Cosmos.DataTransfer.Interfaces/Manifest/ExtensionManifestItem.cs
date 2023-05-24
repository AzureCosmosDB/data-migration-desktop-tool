namespace Cosmos.DataTransfer.Interfaces.Manifest;

public record ExtensionManifestItem(string Name, ExtensionDirection Direction, IEnumerable<ExtensionSettingProperty> Settings)
{

}