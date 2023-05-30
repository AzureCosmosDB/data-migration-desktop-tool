using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Azure.Identity;
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

                ThroughputProperties? throughputProperties = settings.IsServerlessAccount
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

            var timer = Stopwatch.StartNew();
            void ReportCount(int i)
            {
                addedCount += i;
                if (addedCount % 500 == 0)
                {
                    logger.LogInformation("{AddedCount} records added after {TotalSeconds}s", addedCount, $"{timer.ElapsedMilliseconds / 1000.0:F2}");
                }
            }

            var convertedObjects = dataItems.Select(di => BuildObject(di, true)).Where(o => o != null).OfType<ExpandoObject>();
            var batches = convertedObjects.Buffer(settings.BatchSize);
            var retry = GetRetryPolicy(settings.MaxRetryCount, settings.InitialRetryDurationMs);
            await foreach (var batch in batches.WithCancellation(cancellationToken))
            {
                var addTasks = batch.Select(item => AddItemAsync(container, item, settings.PartitionKeyPath ?? settings.PartitionKeyPaths?.FirstOrDefault(), settings.WriteMode, retry, logger, cancellationToken)).ToList();

                var results = await Task.WhenAll(addTasks);
                ReportCount(results.Sum());
            }

            logger.LogInformation("Added {AddedCount} total records in {TotalSeconds}s", addedCount, $"{timer.ElapsedMilliseconds / 1000.0:F2}");
        }

        private static string StripSpecialChars(string displayName)
        {
            return Regex.Replace(displayName, "[^\\w]", "", RegexOptions.Compiled);
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

        private static Task<int> AddItemAsync(Container container, ExpandoObject item, string? partitionKeyPath, DataWriteMode mode, AsyncRetryPolicy retryPolicy, ILogger logger, CancellationToken cancellationToken)
        {
            logger.LogTrace("Adding item {Id}", GetPropertyValue(item, "id"));
            var task = retryPolicy.ExecuteAsync(() =>
                {
                    switch (mode)
                    {
                        case DataWriteMode.InsertStream:
                            ArgumentNullException.ThrowIfNull(partitionKeyPath, nameof(partitionKeyPath));
                            return container.CreateItemStreamAsync(CreateItemStream(item), new PartitionKey(GetPropertyValue(item, partitionKeyPath.TrimStart('/'))), cancellationToken: cancellationToken);
                        case DataWriteMode.Insert:
                            return container.CreateItemAsync(item, cancellationToken: cancellationToken);
                        case DataWriteMode.UpsertStream:
                            ArgumentNullException.ThrowIfNull(partitionKeyPath, nameof(partitionKeyPath));
                            return container.UpsertItemStreamAsync(CreateItemStream(item), new PartitionKey(GetPropertyValue(item, partitionKeyPath.TrimStart('/'))), cancellationToken: cancellationToken);
                        case DataWriteMode.Upsert:
                            return container.UpsertItemAsync(item, cancellationToken: cancellationToken);
                    }

                    throw new ArgumentOutOfRangeException(nameof(mode), $"Invalid data write mode specified: {mode}");
                })
                .ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully)
                    {
                        return 1;
                    }

                    if (t.IsFaulted)
                    {
                        logger.LogWarning(t.Exception, "Error adding record: {ErrorMessage}", t.Exception?.Message);
                    }

                    return 0;
                }, cancellationToken);
            return task;
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

        private static ExpandoObject? BuildObject(IDataItem? source, bool requireStringId = false)
        {
            if (source == null)
                return null;

            var fields = source.GetFieldNames().ToList();
            var item = new ExpandoObject();
            if (requireStringId && !fields.Contains("id", StringComparer.CurrentCultureIgnoreCase))
            {
                item.TryAdd("id", Guid.NewGuid().ToString());
            }
            foreach (string field in fields)
            {
                object? value = source.GetValue(field);
                var fieldName = field;
                if (string.Equals(field, "id", StringComparison.CurrentCultureIgnoreCase) && requireStringId)
                {
                    value = value?.ToString();
                    fieldName = "id";
                }
                else if (value is IDataItem child)
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
            yield return new CosmosSinkSettings();
        }
    }
}