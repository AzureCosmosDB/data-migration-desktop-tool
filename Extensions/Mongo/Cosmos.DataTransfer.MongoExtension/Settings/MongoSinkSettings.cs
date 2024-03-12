using System.ComponentModel.DataAnnotations;

namespace Cosmos.DataTransfer.MongoExtension.Settings;
public class MongoSinkSettings : MongoBaseSettings
{
    [Required]
    public string? Collection { get; set; }

    public int? BatchSize { get; set; }

    public bool? GenerateEmbedding { get; set; }

    public string? OpenAIUrl { get; set; }
    public string? OpenAIKey { get; set; }
    
    // name of the deployment for text-embedding-ada-002
    public string? OpenAIDeploymentName { get; set; } 
    public string? SourcePropEmbedding { get; set; }
    public string? DestPropEmbedding { get; set; }    
}
