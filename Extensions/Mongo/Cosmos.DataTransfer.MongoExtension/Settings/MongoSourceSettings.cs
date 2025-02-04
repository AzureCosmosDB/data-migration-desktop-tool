using Cosmos.DataTransfer.Interfaces.Manifest;

namespace Cosmos.DataTransfer.MongoExtension.Settings;
public class MongoSourceSettings : MongoBaseSettings
{
    public string? Collection { get; set; }

    [SensitiveValue]
    public Dictionary<string, IReadOnlyDictionary<string, object>>? KMSProviders { get; set; }

    public string? KeyVaultNamespace { get; set; }
}
