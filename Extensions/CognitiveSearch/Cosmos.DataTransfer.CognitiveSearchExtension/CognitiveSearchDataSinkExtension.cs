using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using Cosmos.DataTransfer.CognitiveSearchExtension.Settings;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Dynamic;

namespace Cosmos.DataTransfer.CognitiveSearchExtension
{
    [Export(typeof(IDataSinkExtension))]
    public class CognitiveSearchDataSinkExtension : IDataSinkExtensionWithSettings
    {
        public string DisplayName => "CognitiveSearch";

        public async Task WriteAsync(IAsyncEnumerable<IDataItem> dataItems, IConfiguration config, IDataSourceExtension dataSource, ILogger logger, CancellationToken cancellationToken = default)
        {
            var settings = config.Get<CognitiveSearchDataSinkSettings>();
            settings.Validate();

            var indexClient = new SearchIndexClient(new Uri(settings.Endpoint!), new AzureKeyCredential(settings.ApiKey!));
            var searchClient = indexClient.GetSearchClient(settings.Index);

            var convertedObjects = dataItems.Select(di => BuildObject(di)).Where(o => o != null).OfType<ExpandoObject>();
            var batches = convertedObjects.Buffer(settings.BatchSize);

            int totalSucceededCount = 0;
            int totalFailedCount = 0;
            var timer = Stopwatch.StartNew();
            await foreach (var batch in batches.WithCancellation(cancellationToken))
            {
                var result = await searchClient.IndexDocumentsAsync(
                    settings.IndexAction switch
                    {
                        IndexActionType.Upload => IndexDocumentsBatch.Upload(batch),
                        IndexActionType.Delete => IndexDocumentsBatch.Delete(batch),
                        IndexActionType.Merge => IndexDocumentsBatch.Merge(batch),
                        IndexActionType.MergeOrUpload => IndexDocumentsBatch.MergeOrUpload(batch),
                        _ => throw new InvalidOperationException()
                    }
                , cancellationToken: cancellationToken);

                var succeededCount = result.Value.Results.Count(r => r.Succeeded);
                var failedCount = result.Value.Results.Count(r => !r.Succeeded);
                totalSucceededCount += succeededCount;
                totalFailedCount += failedCount;

                logger.LogInformation("Succeeded {Succeeded},Faild {Failed} documents indexed after {TotalSeconds}s", succeededCount, failedCount, $"{timer!.ElapsedMilliseconds / 1000.0:F2}");
                foreach (var r in result.Value.Results.Where(r => !r.Succeeded))
                {
                    logger.LogWarning("Key:{Key},Status:{Status},ErrorMessage{ErrorMessage}", r.Key, r.Status, r.ErrorMessage);
                }
            }

            logger.LogInformation("Succeeded {Succeeded},Faild {Failed} documents indexed in {TotalSeconds}s", totalSucceededCount, totalFailedCount, $"{timer.ElapsedMilliseconds / 1000.0:F2}");
        }

        private static ExpandoObject? BuildObject(IDataItem? source)
        {
            if (source == null)
                return null;

            var fields = source.GetFieldNames().ToList();
            var item = new ExpandoObject();
            foreach (string field in fields)
            {
                object? value = source.GetValue(field);
                var fieldName = field;
                if (value is IDataItem child)
                {
                    value = BuildObject(child);
                }
                else if (value is IEnumerable<object?> array)
                {
                    value = array.Select(dataItem =>
                    {
                        if (dataItem is IDataItem childObject)
                        {
                            return BuildObject(childObject);
                        }
                        return dataItem;
                    }).ToArray();
                }

                item.TryAdd(fieldName, value);
            }

            return item;
        }

        public IEnumerable<IDataExtensionSettings> GetSettings()
        {
            yield return new CognitiveSearchDataSinkSettings();
        }
    }
}
