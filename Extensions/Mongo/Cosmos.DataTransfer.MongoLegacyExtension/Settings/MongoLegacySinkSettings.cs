using System.ComponentModel.DataAnnotations;

namespace Cosmos.DataTransfer.MongoLegacyExtension.Settings;
public class MongoLegacySinkSettings : MongoLegacyBaseSettings
{
    [Required]
    public string? Collection { get; set; }

    public int? BatchSize { get; set; }

    public string? IdFieldName { get; set; }
}