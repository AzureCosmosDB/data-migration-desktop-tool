using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.Net;
using System.Numerics;
using System.Runtime.ExceptionServices;
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

            using var client = CosmosExtensionServices.CreateClient(settings!, DisplayName, logger, dataSource.DisplayName);

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

            var convertedObjects = dataItems
                .Select(di => di.BuildDynamicObjectTree(requireStringId: true, ignoreNullValues: settings.IgnoreNullValues, preserveMixedCaseIds: settings.PreserveMixedCaseIds, transformations: settings.Transformations))
                .Where(o => o != null)
                .OfType<ExpandoObject>();
            await WriteItemsAsync(container, convertedObjects, settings, logger, cancellationToken);
        }

        internal static async Task WriteItemsAsync(Container container, IAsyncEnumerable<ExpandoObject> items,
            CosmosSinkSettings settings, ILogger logger, CancellationToken cancellationToken)
        {
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

            var batches = items.Buffer(settings.BatchSize);
            IReadOnlyList<string> partitionKeyPaths = settings.PartitionKeyPaths is not null
                ? settings.PartitionKeyPaths
                : settings.PartitionKeyPath is not null ? new[] { settings.PartitionKeyPath } : Array.Empty<string>();
            var retry = GetRetryPolicy(settings.MaxRetryCount, settings.InitialRetryDurationMs);
            try
            {
                await foreach (var batch in batches.WithCancellation(cancellationToken))
                {
                    inputCount += batch.Count;
                    await WriteBatchAsync(container, batch, partitionKeyPaths,
                        settings.WriteMode, retry, logger, ReportCount, cancellationToken);
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning("Cosmos sink stopped after {AddedCount} successful writes out of {TotalCount} records in started batches. Other records may be unattempted or have an unknown outcome.", addedCount, inputCount);
                if (exception is CosmosException cosmosException)
                {
                    throw new DataTransferException(
                        $"Cosmos sink {settings.WriteMode} failed with status {(int)cosmosException.StatusCode} and substatus {cosmosException.SubStatusCode}. See preceding retry and progress messages.",
                        cosmosException);
                }
                throw;
            }

            if (addedCount != inputCount)
            {
                logger.LogWarning("Added {AddedCount} of {TotalCount} total records in {TotalSeconds}s", addedCount, inputCount, $"{timer.ElapsedMilliseconds / 1000.0:F2}");
                throw new Exception($"Only {addedCount} of {inputCount} records were added to Cosmos");
            }

            logger.LogInformation("Added {AddedCount} total records in {TotalSeconds}s ({AddRate} records/s)", addedCount, $"{timer.ElapsedMilliseconds / 1000.0:F2}", $"{(int)(addedCount / (timer.ElapsedMilliseconds / 1000.0))}");
        }

        internal static async Task WriteBatchAsync(Container container, IEnumerable<ExpandoObject> batch,
            IReadOnlyList<string> partitionKeyPaths, DataWriteMode mode, AsyncRetryPolicy retryPolicy, ILogger logger,
            Action<int> reportCount, CancellationToken cancellationToken)
        {
            using var batchCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            ExceptionDispatchInfo? failure = null;
            int successfulCount = 0;
            int startedCount = 0;
            async Task WriteItem(ExpandoObject item)
            {
                Interlocked.Increment(ref startedCount);
                try
                {
                    var result = await AddItemAsync(container, item, partitionKeyPaths, mode, retryPolicy, logger, batchCancellation.Token);
                    if (!result.IsSuccess)
                    {
                        throw new InvalidOperationException($"Cosmos sink {mode} failed with status {(int)result.StatusCode}.");
                    }
                    Interlocked.Add(ref successfulCount, result.ItemCount);
                }
                catch (Exception exception)
                {
                    if (Interlocked.CompareExchange(ref failure, ExceptionDispatchInfo.Capture(exception), null) == null)
                    {
                        await batchCancellation.CancelAsync();
                    }
                    throw;
                }
            }

            try
            {
                await Task.WhenAll(batch.Select(WriteItem).ToArray());
            }
            catch
            {
                logger.LogWarning("Cosmos sink batch stopped with {SuccessfulCount} confirmed successful writes out of {StartedCount} records. Remaining outcomes may be unknown.", successfulCount, startedCount);
                // All tasks have drained; sibling cancellation must not replace the original failure.
                failure?.Throw();
                throw;
            }
            finally
            {
                reportCount(successfulCount);
            }
        }

        internal static AsyncRetryPolicy GetRetryPolicy(int maxRetryCount, int initialRetryDuration,
            Func<int, CosmosException, TimeSpan>? retryDelayProvider = null)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(maxRetryCount);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialRetryDuration);
            return Policy
                .Handle<CosmosException>(c => c.StatusCode == HttpStatusCode.TooManyRequests)
                .WaitAndRetryAsync(maxRetryCount,
                    (attempt, exception, _) => retryDelayProvider != null
                        ? retryDelayProvider(attempt, (CosmosException)exception)
                        : GetRetryDelay((CosmosException)exception, attempt, initialRetryDuration),
                    (_, _, attempt, context) =>
                    {
                        context["RetryCount"] = attempt;
                        return Task.CompletedTask;
                    });
        }

        internal static TimeSpan GetRetryDelay(CosmosException exception, int retryAttempt, int initialRetryDuration)
        {
            // The SDK returns null for headers outside Int64, which must not turn an
            // oversized positive service delay into a short fallback wait.
            if (BigInteger.TryParse(exception.Headers.Get("x-ms-retry-after-ms"), NumberStyles.Number,
                    CultureInfo.InvariantCulture, out var serviceDelayMs) && serviceDelayMs > uint.MaxValue - 1)
            {
                throw new NotSupportedException("Cosmos sink retry-after exceeds the supported delay range; refusing to retry early.");
            }

            TimeSpan? retryAfter;
            try
            {
                retryAfter = exception.RetryAfter;
            }
            catch (OverflowException)
            {
                throw new NotSupportedException("Cosmos sink retry-after exceeds the supported delay range; refusing to retry early.");
            }

            if (retryAfter > TimeSpan.Zero)
            {
                // Task.Delay supports at most uint.MaxValue - 1 milliseconds.
                if (retryAfter.Value.TotalMilliseconds > uint.MaxValue - 1)
                {
                    throw new NotSupportedException("Cosmos sink retry-after exceeds the supported delay range; refusing to retry early.");
                }

                return retryAfter.Value;
            }

            // Only fallback delays are capped. Limit the exponent before calculating it to avoid overflow.
            const int maxFallbackDelayMs = 30_000;
            double exponentialDelayMs = Math.Min(maxFallbackDelayMs,
                initialRetryDuration * Math.Pow(2, Math.Min(retryAttempt - 1, 15)));
            int jitterMs = Random.Shared.Next(0, Math.Max(1, Math.Min(initialRetryDuration, maxFallbackDelayMs) / 2));
            return TimeSpan.FromMilliseconds(Math.Min(maxFallbackDelayMs, exponentialDelayMs + jitterMs));
        }

        internal static async Task<ItemResult> AddItemAsync(Container container, ExpandoObject item, IReadOnlyList<string> partitionKeyPaths, DataWriteMode mode, AsyncRetryPolicy retryPolicy, ILogger logger, CancellationToken cancellationToken)
        {
            string? id = GetPropertyValue(item, "id");
            var context = new Context();
            try
            {
                var result = await retryPolicy.ExecuteAsync(
                    (_, token) => PopulateItem(container, item, partitionKeyPaths, mode, id, token),
                    context, cancellationToken);
                if (!result.IsSuccess)
                {
                    logger.LogWarning("Cosmos sink {WriteMode} failed with status {StatusCode}.", mode, (int)result.StatusCode);
                }
                return result;
            }
            catch (CosmosException exception)
            {
                var retries = context.TryGetValue("RetryCount", out var count) ? (int)count : 0;
                if (exception.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    logger.LogWarning("Cosmos sink {WriteMode} exhausted {RetryCount} additional application retries with status 429. SDK retries also apply; consider reducing BatchSize or increasing available throughput.", mode, retries);
                }
                else
                {
                    logger.LogWarning("Cosmos sink {WriteMode} failed with status {StatusCode}; not retried by the application.", mode, (int)exception.StatusCode);
                }
                throw;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning("Cosmos sink {WriteMode} failed with {ErrorType}.", mode, exception.GetType().Name);
                throw;
            }
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
                    using (var insertMessage = await container.CreateItemStreamAsync(itemStream, partitionKey, cancellationToken: cancellationToken))
                    {
                        statusCode = GetStreamStatusCode(insertMessage);
                    }
                    break;
                case DataWriteMode.Insert:
                    var insertResponse = await container.CreateItemAsync(item, cancellationToken: cancellationToken);
                    statusCode = insertResponse.StatusCode;
                    break;
                case DataWriteMode.UpsertStream:
                    using (var upsertMessage = await container.UpsertItemStreamAsync(itemStream, partitionKey, cancellationToken: cancellationToken))
                    {
                        statusCode = GetStreamStatusCode(upsertMessage);
                    }
                    break;
                case DataWriteMode.Upsert:
                    var upsertResponse = await container.UpsertItemAsync(item, cancellationToken: cancellationToken);
                    statusCode = upsertResponse.StatusCode;
                    break;
                case DataWriteMode.DeleteStream:
                    using (var deleteMessage = await container.DeleteItemStreamAsync(itemId, partitionKey, cancellationToken: cancellationToken))
                    {
                        statusCode = GetStreamStatusCode(deleteMessage);
                    }
                    break;
                case DataWriteMode.Delete:
                    try
                    {
                        var deleteResponse = await container.DeleteItemAsync<ExpandoObject>(itemId, partitionKey, cancellationToken: cancellationToken);
                        statusCode = deleteResponse.StatusCode;
                    }
                    catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound && exception.SubStatusCode == 0)
                    {
                        statusCode = HttpStatusCode.NotFound;
                    }
                    break;
            }

            if (statusCode == null)
            {
                throw new ArgumentOutOfRangeException(nameof(mode), $"Invalid data write mode specified: {mode}");
            }

            return new ItemResult(itemId, mode, statusCode.Value);
        }

        private static HttpStatusCode GetStreamStatusCode(ResponseMessage response)
        {
            // Stream APIs return service failures instead of throwing.
            // A nonzero (or malformed) 404 substatus is not proof that the item is absent.
            var subStatus = response.Headers.Get("x-ms-substatus");
            if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                (response.StatusCode == HttpStatusCode.NotFound && subStatus != null &&
                 (!int.TryParse(subStatus, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code) || code != 0)))
            {
                response.EnsureSuccessStatusCode();
            }
            return response.StatusCode;
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
            return TryGetPropertyValue(item, propertyName, out var value) ? value?.ToString() : null;
        }

        private static bool TryGetPropertyValue(ExpandoObject item, string propertyName, out object? value)
        {
            // Handle nested property paths (e.g., "property1/property2/property3")
            // Note: Calling code uses TrimStart('/') to remove leading slash before calling this method
            var pathSegments = propertyName.Split('/');
            object? current = item;
            value = null;
            
            foreach (var segment in pathSegments)
            {
                if (current == null)
                {
                    return false;
                }
                
                if (current is not ExpandoObject expandoObj)
                {
                    return false;
                }
                
                var dict = (IDictionary<string, object?>)expandoObj;
                if (!dict.TryGetValue(segment, out current))
                {
                    return false;
                }
            }
            
            value = current;
            return true;
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
