using System.ComponentModel.DataAnnotations;
using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Interfaces.Manifest;

namespace Cosmos.DataTransfer.MongoExtension.Settings;
public class MongoBaseSettings : IDataExtensionSettings
{
    [Required]
    [SensitiveValue]
    public string? ConnectionString { get; set; }

    [Required]
    public string? DatabaseName { get; set; }
}
