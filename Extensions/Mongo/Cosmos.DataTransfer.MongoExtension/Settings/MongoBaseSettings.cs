using System.ComponentModel.DataAnnotations;
using Cosmos.DataTransfer.Interfaces;

namespace Cosmos.DataTransfer.MongoExtension.Settings;
public class MongoBaseSettings : IDataExtensionSettings
{
    [Required]
    public string? ConnectionString { get; set; }

    [Required]
    public string? DatabaseName { get; set; }
}
