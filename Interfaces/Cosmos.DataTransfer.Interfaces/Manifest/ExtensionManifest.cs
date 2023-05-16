namespace Cosmos.DataTransfer.Interfaces.Manifest;

public record ExtensionManifest(IEnumerable<ExtensionManifestItem> Extensions)
{
    public static readonly ExtensionManifest Empty = new(Enumerable.Empty<ExtensionManifestItem>());
}