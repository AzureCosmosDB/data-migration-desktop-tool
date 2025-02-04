using System.ComponentModel.Composition;
using Azure;
using Azure.AI.OpenAI;
using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.MongoExtension;
using Cosmos.DataTransfer.MongoVectorExtension.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

namespace Cosmos.DataTransfer.MongoVectorExtension;
[Export(typeof(IDataSinkExtension))]
public class MongoVectorDataSinkExtension : IDataSinkExtensionWithSettings
{
    public string DisplayName => $"MongoDB-Vector{ExtensionExtensions.BetaExtensionTag}";

    public async Task WriteAsync(IAsyncEnumerable<IDataItem> dataItems, IConfiguration config, IDataSourceExtension dataSource, ILogger logger, CancellationToken cancellationToken = default)
    {
        var settings = config.Get<MongoVectorSinkSettings>();        
        settings.Validate();

        if (!string.IsNullOrEmpty(settings.ConnectionString) && !string.IsNullOrEmpty(settings.DatabaseName) && !string.IsNullOrEmpty(settings.Collection))
        {
            var Isembeddingsetsvalid = false;
            var client = new OpenAIClient("");
            if (settings.GenerateEmbedding.HasValue && settings.GenerateEmbedding.Value && settings.SourcePropEmbedding != null && settings.DestPropEmbedding != null)
            {
                if (!string.IsNullOrEmpty(settings.OpenAIUrl) && !string.IsNullOrEmpty(settings.OpenAIKey) && !string.IsNullOrEmpty(settings.OpenAIDeploymentName))
                {
                    client = new OpenAIClient(new Uri(settings.OpenAIUrl), new AzureKeyCredential(settings.OpenAIKey));
                    Isembeddingsetsvalid = true;
                    logger.LogInformation("OpenAI Embedding settings are valid.");
                }                
            }           

            var context = new Context(settings.ConnectionString, settings.DatabaseName);
            var repo = context.GetRepository<BsonDocument>(settings.Collection);
            var batchSize = settings.BatchSize ?? 1000;
            var objects = new List<BsonDocument>();
            int itemCount = 0;            
            await foreach (var item in dataItems.WithCancellation(cancellationToken))
            {
                var dict = item.BuildDynamicObjectTree();

                if (Isembeddingsetsvalid)
                {
                    var valtoemb = item.GetValue(settings.SourcePropEmbedding)?.ToString();
                    if (!string.IsNullOrEmpty(valtoemb) && valtoemb?.Length < 8192)
                    {
                        var options = new EmbeddingsOptions()
                        {
                            DeploymentName = settings.OpenAIDeploymentName,
                            Input = { valtoemb }
                        };
                        var vector = await client.GetEmbeddingsAsync(options,cancellationToken);                        
                        if (vector != null)
                        {
                            dict?.TryAdd(settings.DestPropEmbedding, vector.Value.Data[0].Embedding.ToArray());
                        }
                    }
                }
                objects.Add(new BsonDocument(dict));
                itemCount++;

                if (objects.Count == batchSize)
                {                    
                    await repo.AddRange(objects);
                    logger.LogInformation("Added {ItemCount} items to collection '{Collection}'", itemCount, settings.Collection);
                    objects.Clear();
                }
            }

            if (objects.Any())
            {
                await repo.AddRange(objects);
            }

            if (itemCount > 0)
                logger.LogInformation("Added {ItemCount} total items to collection '{Collection}'", itemCount, settings.Collection);
            else
                logger.LogWarning("No items added to collection '{Collection}'", settings.Collection);
        }
    }

    public IEnumerable<IDataExtensionSettings> GetSettings()
    {
        yield return new MongoVectorSinkSettings();
    }
}
