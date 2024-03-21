using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Dynamic;
using System.Net;
using System.Text;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;

namespace Cosmos.DataTransfer.CosmosExtension
{
    [Export(typeof(IDataSinkExtension))]
    public class CosmosDataSinkExtension : IDataSinkExtensionWithSettings
    {
        public string DisplayName => "Cosmos-nosql";

        public async Task WriteAsync(IAsyncEnumerable<IDataItem> dataItems, IConfiguration config, IDataSourceExtension dataSource, ILogger logger, CancellationToken cancellationToken = default)
        {
            var settings = config.Get<CosmosSinkSettings>();
            settings.Validate();

            var client = CosmosExtensionServices.CreateClient(settings, DisplayName, dataSource.DisplayName);

            Container? container;
            if (settings.UseRbacAuth)
            {
                container = client.GetContainer(settings.Database, settings.Container);
            }
            else
            {
                Database database = await client.CreateDatabaseIfNotExistsAsync(settings.Database, cancellationToken: cancellationToken);

                if (settings.RecreateContainer)
                {
                    try
                    {
                        await database.GetContainer(settings.Container).DeleteContainerAsync(cancellationToken: cancellationToken);
                    }
                    catch { }
                }

                var containerProperties = new ContainerProperties
                {
                    Id = settings.Container,
                    PartitionKeyDefinitionVersion = PartitionKeyDefinitionVersion.V2,
                };

                if (settings.PartitionKeyPaths != null)
                {
                    logger.LogInformation("Using partition key paths: {PartitionKeyPaths}", string.Join(", ", settings.PartitionKeyPaths));
                    containerProperties.PartitionKeyPaths = settings.PartitionKeyPaths;
                }
                else
                {
                    containerProperties.PartitionKeyPath = settings.PartitionKeyPath;
                }

                ThroughputProperties? throughputProperties = settings.IsServerlessAccount || settings.UseSharedThroughput
                    ? null
                    : settings.UseAutoscaleForCreatedContainer
                    ? ThroughputProperties.CreateAutoscaleThroughput(settings.CreatedContainerMaxThroughput ?? 4000)
                    : ThroughputProperties.CreateManualThroughput(settings.CreatedContainerMaxThroughput ?? 400);

                try
                {
                    container = await database.CreateContainerIfNotExistsAsync(containerProperties, throughputProperties, cancellationToken: cancellationToken);
                }
                catch (CosmosException ex) when (ex.ResponseBody.Contains("not supported for serverless accounts", StringComparison.InvariantCultureIgnoreCase))
                {
                    logger.LogWarning("Cosmos Serverless Account does not support throughput options. Creating Container {ContainerName} without those settings.", settings.Container);

                    // retry without throughput settings which are incompatible with serverless
                    container = await database.CreateContainerIfNotExistsAsync(containerProperties, cancellationToken: cancellationToken);
                }
            }

            await CosmosExtensionServices.VerifyContainerAccess(container, settings.Container, logger, cancellationToken);

            int addedCount = 0;
            int inputCount = 0;

            var timer = Stopwatch.StartNew();
            void ReportCount(int i)
            {
                addedCount += i;
                if (addedCount % 500 == 0)
                {
                    logger.LogInformation("{AddedCount} records added after {TotalSeconds}s", addedCount, $"{timer.ElapsedMilliseconds / 1000.0:F2}");
                }
            }

            var convertedObjects = dataItems.Select(di => di.BuildDynamicObjectTree(true)).Where(o => o != null).OfType<ExpandoObject>();
            var batches = convertedObjects.Buffer(settings.BatchSize);
            var retry = GetRetryPolicy(settings.MaxRetryCount, settings.InitialRetryDurationMs);
            await foreach (var batch in batches.WithCancellation(cancellationToken))
            {
                var addTasks = batch.Select(item => AddItemAsync(container, item, settings.PartitionKeyPath ?? settings.PartitionKeyPaths?.FirstOrDefault(), settings.WriteMode, retry, logger, cancellationToken)).ToList();

                var results = await Task.WhenAll(addTasks);
                ReportCount(results.Sum(i => i.ItemCount));
                inputCount += results.Length;
            }

            if (addedCount != inputCount)
            {
                logger.LogWarning("Added {AddedCount} of {TotalCount} total records in {TotalSeconds}s", addedCount, inputCount, $"{timer.ElapsedMilliseconds / 1000.0:F2}");
                throw new Exception($"Only {addedCount} of {inputCount} records were added to Cosmos");
            }

            logger.LogInformation("Added {AddedCount} total records in {TotalSeconds}s", addedCount, $"{timer.ElapsedMilliseconds / 1000.0:F2}");
        }

        private static AsyncRetryPolicy GetRetryPolicy(int maxRetryCount, int initialRetryDuration)
        {
            int retryDelayBaseMs = initialRetryDuration / 2;
            var jitter = new Random();
            var retryPolicy = Policy
                .Handle<CosmosException>(c => c.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                .WaitAndRetryAsync(maxRetryCount,
                    retryAttempt => TimeSpan.FromMilliseconds(Math.Pow(2, retryAttempt) * retryDelayBaseMs + jitter.Next(0, retryDelayBaseMs))
                );

            return retryPolicy;
        }

        private static Task<ItemResult> AddItemAsync(Container container, ExpandoObject item, string? partitionKeyPath, DataWriteMode mode, AsyncRetryPolicy retryPolicy, ILogger logger, CancellationToken cancellationToken)
        {
            string? id = GetPropertyValue(item, "id");
            logger.LogTrace("Adding item {Id}", id);

            var task = retryPolicy.ExecuteAsync(() => PopulateItem(container, item, partitionKeyPath, mode, id, cancellationToken))
                .ContinueWith(t =>
                {
                    bool requestSucceeded = t.Result.IsSuccess;
                    if (t.IsCompletedSuccessfully && requestSucceeded)
                    {
                        return t.Result;
                    }

                    if (t.IsFaulted)
                    {
                        logger.LogWarning(t.Exception, "Error adding record: {ErrorMessage}", t.Exception?.Message);
                    }
                    else if (!requestSucceeded)
                    {
                        logger.LogWarning(t.Exception, "Error adding record {Id}: {ErrorMessage}", t.Result.Id, t.Result.StatusCode);
                        return t.Result;
                    }

                    return new ItemResult(null, mode, HttpStatusCode.InternalServerError);
                }, cancellationToken);
            return task;
        }

        private static async Task<ItemResult> PopulateItem(Container container, ExpandoObject item, string? partitionKeyPath, DataWriteMode mode, string? itemId, CancellationToken cancellationToken)
        {
            HttpStatusCode? statusCode = null;
            switch (mode)
            {
                case DataWriteMode.InsertStream:
                    ArgumentNullException.ThrowIfNull(partitionKeyPath, nameof(partitionKeyPath));
                    var insertMessage = await container.CreateItemStreamAsync(CreateItemStream(item), new PartitionKey(GetPropertyValue(item, partitionKeyPath.TrimStart('/'))), cancellationToken: cancellationToken);
                    statusCode = insertMessage.StatusCode;
                    break;
                case DataWriteMode.Insert:
                    var insertResponse = await container.CreateItemAsync(item, cancellationToken: cancellationToken);
                    statusCode = insertResponse.StatusCode;
                    break;
                case DataWriteMode.UpsertStream:
                    ArgumentNullException.ThrowIfNull(partitionKeyPath, nameof(partitionKeyPath));
                    var upsertMessage = await container.UpsertItemStreamAsync(CreateItemStream(item), new PartitionKey(GetPropertyValue(item, partitionKeyPath.TrimStart('/'))), cancellationToken: cancellationToken);
                    statusCode = upsertMessage.StatusCode;
                    break;
                case DataWriteMode.Upsert:
                    var upsertResponse = await container.UpsertItemAsync(item, cancellationToken: cancellationToken);
                    statusCode = upsertResponse.StatusCode;
                    break;
                case DataWriteMode.DeleteStream:
                    ArgumentNullException.ThrowIfNull(partitionKeyPath, nameof(partitionKeyPath));
                    var deleteMessage = await container.DeleteItemStreamAsync(itemId, new PartitionKey(GetPropertyValue(item, partitionKeyPath.TrimStart('/'))), cancellationToken: cancellationToken);
                    statusCode = deleteMessage.StatusCode;
                    break;
                case DataWriteMode.Delete:
                    ArgumentNullException.ThrowIfNull(partitionKeyPath, nameof(partitionKeyPath));
                    var deleteResponse = await container.DeleteItemAsync<ExpandoObject>(itemId, new PartitionKey(GetPropertyValue(item, partitionKeyPath.TrimStart('/'))), cancellationToken: cancellationToken);
                    statusCode = deleteResponse.StatusCode;
                    break;
            }

            if (statusCode == null)
            {
                throw new ArgumentOutOfRangeException(nameof(mode), $"Invalid data write mode specified: {mode}");
            }

            return new ItemResult(itemId, mode, statusCode.Value);
        }

        private static MemoryStream CreateItemStream(ExpandoObject item)
        {
            var json = JsonConvert.SerializeObject(item);
            return new MemoryStream(Encoding.UTF8.GetBytes(json));
        }

        private static string? GetPropertyValue(ExpandoObject item, string propertyName)
        {
            return ((IDictionary<string, object?>)item)[propertyName]?.ToString();
        }

        public IEnumerable<IDataExtensionSettings> GetSettings()
        {
            yield return new CosmosSinkSettings();
        }

        public record ItemResult(string? Id, DataWriteMode DataWriteMode, HttpStatusCode StatusCode)
        {
            public bool IsSuccess => StatusCode is HttpStatusCode.OK or HttpStatusCode.Created ||
                (StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound && DataWriteMode is DataWriteMode.Delete or DataWriteMode.DeleteStream);
            public int ItemCount => IsSuccess ? 1 : 0;
        }
    }
}
