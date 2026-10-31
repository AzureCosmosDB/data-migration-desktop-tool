using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Dynamic;
using System.Net;
using System.Text;
using System.Text.Json;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Encryption;
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

        /// <summary>
        /// Creates or retrieves a Cosmos DB database and container based on the provided settings.
        /// </summary>
        /// <param name="client">The <see cref="CosmosClient"/> instance used to interact with Cosmos DB.</param>
        /// <param name="settings">The <see cref="CosmosSinkSettings"/> containing configuration for the database and container.</param>
        /// <param name="logger">The <see cref="ILogger"/> instance for logging information and warnings.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>
        /// A <see cref="Container"/> instance representing the created or retrieved Cosmos DB container.
        /// </returns>
        /// <remarks>
        /// This method performs the following actions:
        /// <list type="bullet">
        /// <item>Checks if the database exists and creates it if necessary, applying the specified throughput settings.</item>
        /// <item>Validates and adjusts the database throughput settings to match the configuration in <paramref name="settings"/>.</item>
        /// <item>Deletes and recreates the container if the <see cref="CosmosSinkSettings.RecreateContainer"/> flag is set.</item>
        /// <item>Handles serverless accounts and shared throughput configurations appropriately.</item>
        /// <item>Logs warnings if certain configurations, such as throughput settings, are not supported in specific scenarios (e.g., serverless accounts).</item>
        /// </list>
        /// </remarks>
        /// <exception cref="CosmosException">
        /// Thrown if there is an error interacting with Cosmos DB, such as insufficient permissions or invalid configurations.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if required settings, such as the database or container name, are missing.
        /// </exception>
        private static async Task<Container> CreateDatabaseAndContainerAsync(
            CosmosClient client,
            CosmosSinkSettings settings,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            Database database;

            if (settings.UseSharedThroughput)
            {
                try
                {
                    database = client.GetDatabase(settings.Database);
                    var throughputResponse = await database.ReadThroughputAsync(cancellationToken);
                    var currentThroughput = throughputResponse.Value;

                    if (settings.UseAutoscaleForDatabase && settings.CreatedContainerMaxThroughput.HasValue && currentThroughput != settings.CreatedContainerMaxThroughput)
                    {
                        await database.ReplaceThroughputAsync(
                            ThroughputProperties.CreateAutoscaleThroughput(settings.CreatedContainerMaxThroughput.Value),
                            cancellationToken: cancellationToken);
                    }
                    else if (!settings.UseAutoscaleForDatabase && settings.CreatedContainerMaxThroughput.HasValue && currentThroughput != settings.CreatedContainerMaxThroughput)
                    {
                        await database.ReplaceThroughputAsync(
                            ThroughputProperties.CreateManualThroughput(settings.CreatedContainerMaxThroughput.Value),
                            requestOptions: null,
                            cancellationToken: cancellationToken);
                    }
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    var newThroughputProperties = settings.UseAutoscaleForDatabase
                        ? ThroughputProperties.CreateAutoscaleThroughput(settings.CreatedContainerMaxThroughput ?? 4000)
                        : ThroughputProperties.CreateManualThroughput(settings.CreatedContainerMaxThroughput ?? 400);

                    database = await client.CreateDatabaseIfNotExistsAsync(
                        settings.Database,
                        newThroughputProperties,
                        cancellationToken: cancellationToken);
                }
            }
            else
            {
                database = await client.CreateDatabaseIfNotExistsAsync(settings.Database, cancellationToken: cancellationToken);
            }

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
                return await database.CreateContainerIfNotExistsAsync(containerProperties, throughputProperties, cancellationToken: cancellationToken);
            }
            catch (CosmosException ex) when (ex.ResponseBody.Contains("not supported for serverless accounts", StringComparison.InvariantCultureIgnoreCase))
            {
                logger.LogWarning("Cosmos Serverless Account does not support throughput options. Creating Container {ContainerName} without those settings.", settings.Container);
                return await database.CreateContainerIfNotExistsAsync(containerProperties, cancellationToken: cancellationToken);
            }
        }

        public async Task WriteAsync(IAsyncEnumerable<IDataItem> dataItems, IConfiguration config, IDataSourceExtension dataSource, ILogger logger, CancellationToken cancellationToken = default)
        {
            var settings = config.Get<CosmosSinkSettings>();
            settings.Validate();

            var client = CosmosExtensionServices.CreateClient(settings!, DisplayName, logger, dataSource.DisplayName);

            Container container;
            if (settings!.UseRbacAuth)
            {
                var cosmosContainer = client.GetContainer(settings.Database, settings.Container);
                container = settings.InitClientEncryption
                    ? await cosmosContainer.InitializeEncryptionAsync(cancellationToken)
                    : cosmosContainer;
            }
            else
            {
                container = await CreateDatabaseAndContainerAsync(client, settings, logger, cancellationToken);
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
                    logger.LogInformation("{AddedCount} records added after {TotalSeconds}s ({AddRate} records/s)", addedCount, $"{timer.ElapsedMilliseconds / 1000.0:F2}", $"{(int)(addedCount / (timer.ElapsedMilliseconds / 1000.0))}");
                }
            }

            var convertedObjects = dataItems
                .Select(di => di.BuildDynamicObjectTree(requireStringId: true, ignoreNullValues: settings.IgnoreNullValues, preserveMixedCaseIds: settings.PreserveMixedCaseIds, transformations: settings.Transformations))
                .Where(o => o != null)
                .OfType<ExpandoObject>();
            var batches = convertedObjects.Buffer(settings.BatchSize);
            IReadOnlyList<string> partitionKeyPaths = settings.PartitionKeyPaths is not null
                ? settings.PartitionKeyPaths
                : settings.PartitionKeyPath is not null ? new[] { settings.PartitionKeyPath } : Array.Empty<string>();
            var retry = GetRetryPolicy(settings.MaxRetryCount, settings.InitialRetryDurationMs);
            await foreach (var batch in batches.WithCancellation(cancellationToken))
            {
                var addTasks = batch.Select(item => AddItemAsync(container, item, partitionKeyPaths, settings.WriteMode, retry, logger, cancellationToken)).ToList();

                var results = await Task.WhenAll(addTasks);
                ReportCount(results.Sum(i => i.ItemCount));
                inputCount += results.Length;
            }

            if (addedCount != inputCount)
            {
                logger.LogWarning("Added {AddedCount} of {TotalCount} total records in {TotalSeconds}s", addedCount, inputCount, $"{timer.ElapsedMilliseconds / 1000.0:F2}");
                throw new Exception($"Only {addedCount} of {inputCount} records were added to Cosmos");
            }

            logger.LogInformation("Added {AddedCount} total records in {TotalSeconds}s ({AddRate} records/s)", addedCount, $"{timer.ElapsedMilliseconds / 1000.0:F2}", $"{(int)(addedCount / (timer.ElapsedMilliseconds / 1000.0))}");
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

        private static Task<ItemResult> AddItemAsync(Container container, ExpandoObject item, IReadOnlyList<string> partitionKeyPaths, DataWriteMode mode, AsyncRetryPolicy retryPolicy, ILogger logger, CancellationToken cancellationToken)
        {
            string? id = GetPropertyValue(item, "id");
            logger.LogTrace("Adding item {Id}", id);

            var task = retryPolicy.ExecuteAsync(() => PopulateItem(container, item, partitionKeyPaths, mode, id, cancellationToken))
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

        private static async Task<ItemResult> PopulateItem(Container container, ExpandoObject item, IReadOnlyList<string> partitionKeyPaths, DataWriteMode mode, string? itemId, CancellationToken cancellationToken)
        {
            using var itemStream = mode is DataWriteMode.InsertStream or DataWriteMode.UpsertStream or DataWriteMode.DeleteStream or DataWriteMode.Delete
                ? CreateItemStream(item)
                : null;
            var partitionKey = itemStream is not null ? CreatePartitionKey(itemStream, partitionKeyPaths) : default;
            HttpStatusCode? statusCode = null;
            switch (mode)
            {
                case DataWriteMode.InsertStream:
                    var insertMessage = await container.CreateItemStreamAsync(itemStream, partitionKey, cancellationToken: cancellationToken);
                    statusCode = insertMessage.StatusCode;
                    break;
                case DataWriteMode.Insert:
                    var insertResponse = await container.CreateItemAsync(item, cancellationToken: cancellationToken);
                    statusCode = insertResponse.StatusCode;
                    break;
                case DataWriteMode.UpsertStream:
                    var upsertMessage = await container.UpsertItemStreamAsync(itemStream, partitionKey, cancellationToken: cancellationToken);
                    statusCode = upsertMessage.StatusCode;
                    break;
                case DataWriteMode.Upsert:
                    var upsertResponse = await container.UpsertItemAsync(item, cancellationToken: cancellationToken);
                    statusCode = upsertResponse.StatusCode;
                    break;
                case DataWriteMode.DeleteStream:
                    var deleteMessage = await container.DeleteItemStreamAsync(itemId, partitionKey, cancellationToken: cancellationToken);
                    statusCode = deleteMessage.StatusCode;
                    break;
                case DataWriteMode.Delete:
                    var deleteResponse = await container.DeleteItemAsync<ExpandoObject>(itemId, partitionKey, cancellationToken: cancellationToken);
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
            var json = JsonConvert.SerializeObject(item, RawJsonCosmosSerializer.GetDefaultSettings());
            return new MemoryStream(Encoding.UTF8.GetBytes(json));
        }

        private static PartitionKey CreatePartitionKey(MemoryStream itemStream, IReadOnlyList<string> partitionKeyPaths)
        {
            if (partitionKeyPaths.Count == 0)
            {
                throw new ArgumentException("At least one partition key path is required.", nameof(partitionKeyPaths));
            }

            // Read the serialized JSON so dates, numeric conversions, and omitted fields match the payload.
            using var document = JsonDocument.Parse(itemStream, new JsonDocumentOptions { MaxDepth = 128 });
            itemStream.Position = 0;
            var builder = new PartitionKeyBuilder();
            foreach (var path in partitionKeyPaths)
            {
                var value = document.RootElement;
                foreach (var segment in ParsePartitionKeyPath(path))
                {
                    if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
                    {
                        value = default;
                        break;
                    }
                }

                switch (value.ValueKind)
                {
                    case JsonValueKind.Undefined:
                        builder.AddNoneType();
                        break;
                    case JsonValueKind.Null:
                        builder.AddNullValue();
                        break;
                    case JsonValueKind.String:
                        builder.Add(value.GetString());
                        break;
                    case JsonValueKind.Number:
                        builder.Add(value.GetDouble());
                        break;
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        builder.Add(value.GetBoolean());
                        break;
                    default:
                        throw new InvalidOperationException($"Partition key path '{path}' must resolve to a string, number, boolean, null, or missing value; found {value.ValueKind}.");
                }
            }

            return builder.Build();
        }

        private static IReadOnlyList<string> ParsePartitionKeyPath(string path)
        {
            var segments = new List<string>();
            int position = 0;
            while (position < path.Length)
            {
                if (path[position] != '/')
                    throw new ArgumentException($"Invalid partition key path at position {position}.", nameof(path));
                position++;
                if (position == path.Length)
                    break;

                char quote = path[position];
                if (quote is '"' or '\'')
                {
                    int start = ++position;
                    while (position < path.Length && (path[position] != quote || path[position - 1] == '\\'))
                        position++;
                    if (position == path.Length)
                        throw new ArgumentException("Partition key path contains an unterminated quoted segment.", nameof(path));

                    // SDK 3.49 PathParser retains escape characters inside quoted segments.
                    segments.Add(path[start..position]);
                    position++;
                }
                else
                {
                    int start = position;
                    while (position < path.Length && path[position] != '/')
                        position++;
                    segments.Add(path[start..position].Trim());
                }
            }

            return segments;
        }

        private static string? GetPropertyValue(ExpandoObject item, string propertyName)
        {
            // Handle nested property paths (e.g., "property1/property2/property3")
            // Note: Calling code uses TrimStart('/') to remove leading slash before calling this method
            var pathSegments = propertyName.Split('/');
            object? current = item;
            
            foreach (var segment in pathSegments)
            {
                if (current == null)
                {
                    return null;
                }
                
                if (current is not ExpandoObject expandoObj)
                {
                    return null;
                }
                
                var dict = (IDictionary<string, object?>)expandoObj;
                if (!dict.TryGetValue(segment, out current))
                {
                    return null;
                }
            }
            
            return current?.ToString();
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
