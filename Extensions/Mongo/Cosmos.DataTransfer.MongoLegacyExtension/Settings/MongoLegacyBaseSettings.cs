using System.ComponentModel.DataAnnotations;
using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Interfaces.Manifest;

namespace Cosmos.DataTransfer.MongoLegacyExtension.Settings;
public class MongoLegacyBaseSettings : IDataExtensionSettings
{
    [Required]
    [SensitiveValue]
    public string? ConnectionString { get; set; }

    [Required]
    public string? DatabaseName { get; set; }
}