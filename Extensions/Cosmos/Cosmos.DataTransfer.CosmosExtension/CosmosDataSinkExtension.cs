using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.Net;
using System.Numerics;
using System.Text;
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

            logger.LogInformation("Added {AddedCount} total records in {TotalSeconds}s ({AddRate} records/s)", addedCount, $"{timer.ElapsedMilliseconds / 1000.0:F2}", $"{(int)(addedCount / (timer.ElapsedMilliseconds / 1000.0))}");
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
                // Task.Delay on .NET 8 supports at most uint.MaxValue - 1 milliseconds.
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

        internal static async Task<ItemResult> AddItemAsync(Container container, ExpandoObject item, string? partitionKeyPath, DataWriteMode mode, AsyncRetryPolicy retryPolicy, ILogger logger, CancellationToken cancellationToken)
        {
            string? id = GetPropertyValue(item, "id");
            var context = new Context();
            try
            {
                var result = await retryPolicy.ExecuteAsync(
                    (_, token) => PopulateItem(container, item, partitionKeyPath, mode, id, token),
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

        private static async Task<ItemResult> PopulateItem(Container container, ExpandoObject item, string? partitionKeyPath, DataWriteMode mode, string? itemId, CancellationToken cancellationToken)
        {
            HttpStatusCode? statusCode = null;
            switch (mode)
            {
                case DataWriteMode.InsertStream:
                    ArgumentNullException.ThrowIfNull(partitionKeyPath, nameof(partitionKeyPath));
                    using (var payload = CreateItemStream(item))
                    using (var insertMessage = await container.CreateItemStreamAsync(payload, new PartitionKey(GetPropertyValue(item, partitionKeyPath.TrimStart('/'))), cancellationToken: cancellationToken))
                    {
                        statusCode = GetStreamStatusCode(insertMessage);
                    }
                    break;
                case DataWriteMode.Insert:
                    var insertResponse = await container.CreateItemAsync(item, cancellationToken: cancellationToken);
                    statusCode = insertResponse.StatusCode;
                    break;
                case DataWriteMode.UpsertStream:
                    ArgumentNullException.ThrowIfNull(partitionKeyPath, nameof(partitionKeyPath));
                    using (var payload = CreateItemStream(item))
                    using (var upsertMessage = await container.UpsertItemStreamAsync(payload, new PartitionKey(GetPropertyValue(item, partitionKeyPath.TrimStart('/'))), cancellationToken: cancellationToken))
                    {
                        statusCode = GetStreamStatusCode(upsertMessage);
                    }
                    break;
                case DataWriteMode.Upsert:
                    var upsertResponse = await container.UpsertItemAsync(item, cancellationToken: cancellationToken);
                    statusCode = upsertResponse.StatusCode;
                    break;
                case DataWriteMode.DeleteStream:
                    ArgumentNullException.ThrowIfNull(partitionKeyPath, nameof(partitionKeyPath));
                    using (var deleteMessage = await container.DeleteItemStreamAsync(itemId, new PartitionKey(GetPropertyValue(item, partitionKeyPath.TrimStart('/'))), cancellationToken: cancellationToken))
                    {
                        statusCode = GetStreamStatusCode(deleteMessage);
                    }
                    break;
                case DataWriteMode.Delete:
                    ArgumentNullException.ThrowIfNull(partitionKeyPath, nameof(partitionKeyPath));
                    try
                    {
                        var deleteResponse = await container.DeleteItemAsync<ExpandoObject>(itemId, new PartitionKey(GetPropertyValue(item, partitionKeyPath.TrimStart('/'))), cancellationToken: cancellationToken);
                        statusCode = deleteResponse.StatusCode;
                    }
                    catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
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
            // Stream APIs return service failures instead of throwing. Normalize only throttling
            // so other status handling (including delete-not-found success) remains unchanged.
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
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
