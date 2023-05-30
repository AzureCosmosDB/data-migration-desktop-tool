namespace Cosmos.DataTransfer.Interfaces.Manifest;

public record ExtensionManifest(string AppVersion, IEnumerable<ExtensionManifestItem> Extensions)
{
    public static readonly ExtensionManifest Empty = new("", Enumerable.Empty<ExtensionManifestItem>());
}