using System.ComponentModel.DataAnnotations;

namespace Cosmos.DataTransfer.MongoExtension.Settings;
public class MongoSinkSettings : MongoBaseSettings
{
    [Required]
    public string? Collection { get; set; }

    public int? BatchSize { get; set; }
}
