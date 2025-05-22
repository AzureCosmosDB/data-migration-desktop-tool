using Cosmos.DataTransfer.Interfaces.Manifest;

namespace Cosmos.DataTransfer.MongoLegacyExtension.Settings;
public class MongoLegacySourceSettings : MongoLegacyBaseSettings
{
    public string? Collection { get; set; }
}