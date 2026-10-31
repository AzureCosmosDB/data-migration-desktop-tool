using System.Text;
using Cosmos.DataTransfer.Common.UnitTests;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Azure.Cosmos;
using System.Net;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel.Composition.Hosting;
using Cosmos.DataTransfer.Core;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cosmos.DataTransfer.CosmosExtension.UnitTests;

[TestClass]
public class CosmosDataSourceExtensionTests
{
    private static Database? testDatabase;
    private static CosmosClient? cosmosClient;
    private static string? connectionString;

    private static bool IsVNext =>
        string.Equals(Environment.GetEnvironmentVariable("Cosmos_EmulatorVariant"), "vnext", StringComparison.OrdinalIgnoreCase);

    private static void RequireMissingNullConformance()
    {
        if (IsVNext)
        {
            Assert.Inconclusive("[vNext missing/null capability] The preview aliases undefined and null partition keys and accepts mismatched key headers, independently reproduced with regular SDK writes. Strict conformance runs on current Linux and Windows.");
        }
    }

    [ClassInitialize]
    public static async Task Initialize(TestContext context) {
        string? endpoint = Environment.GetEnvironmentVariable("Cosmos_Endpoint");
        string? accountKey = Environment.GetEnvironmentVariable("Cosmos_Key");

        if (string.IsNullOrWhiteSpace(endpoint) && string.IsNullOrWhiteSpace(accountKey)) {
            Assert.Inconclusive("Set Cosmos_Endpoint and Cosmos_Key to run Cosmos DB integration tests.");
        }

        Assert.IsFalse(string.IsNullOrWhiteSpace(endpoint), "Cosmos_Endpoint is required when Cosmos_Key is configured.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(accountKey), "Cosmos_Key is required when Cosmos_Endpoint is configured.");
        
        connectionString = $"AccountEndpoint={endpoint};AccountKey={accountKey};";

        var fullname = typeof(CosmosDataSourceExtensionTests).Assembly.ManifestModule.Name;
        var dbname = typeof(CosmosDataSourceExtensionTests).Name + "-" + Guid.NewGuid().ToString().Substring(0, 8);

        cosmosClient = new CosmosClient(
            accountEndpoint: endpoint,
            authKeyOrResourceToken: accountKey,
            new CosmosClientOptions {
                ApplicationName = fullname,
                ConnectionMode = ConnectionMode.Gateway,
                LimitToEndpoint = true,
                RequestTimeout = TimeSpan.FromSeconds(10)
            }
        );

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        testDatabase = await cosmosClient.CreateDatabaseAsync(dbname, cancellationToken: timeout.Token);
    }

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static async Task CleanUp() {
        try {
            if (testDatabase is not null) {
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                await testDatabase.DeleteAsync(cancellationToken: timeout.Token);
            }
        } catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound) {
            // Ignore; the database has already been deleted.
        } finally {
            cosmosClient?.Dispose();
        }
    }

    private static (CosmosClient client, Database database, string connectionString) GetSetup() {
        return (
            cosmosClient ?? throw new InvalidOperationException("Cosmos DB client was not initialized."),
            testDatabase ?? throw new InvalidOperationException("Cosmos DB test database was not initialized."),
            connectionString ?? throw new InvalidOperationException("Cosmos DB connection string was not initialized."));
    }

    [TestMethod]
    public async Task Test_SdkPartitionKeys_DistinguishMissingAndNull()
    {
        RequireMissingNullConformance();
        var (_, database, _) = GetSetup();
        var failures = new List<string>();
        foreach (var nested in new[] { false, true })
        {
            var path = nested ? "/nested/key" : "/partitionKey";
            var container = (await database.CreateContainerAsync(
                nested ? "SdkNestedKeyControl" : "SdkKeyControl", path, throughput: 400)).Container;
            var missing = new Dictionary<string, object?> { ["id"] = "missing" };
            var explicitNull = new Dictionary<string, object?> { ["id"] = "null" };
            explicitNull[nested ? "nested" : "partitionKey"] = nested
                ? new Dictionary<string, object?> { ["key"] = null }
                : null;

            // Use the SDK's default serializer and inferred keys, without the DMT sink or its batching.
            await CheckWrite(missing, "missing write");
            await CheckWrite(explicitNull, "null write with distinct id");
            await CheckRead("missing", PartitionKey.None, HttpStatusCode.OK, "missing read with undefined");
            await CheckRead("null", PartitionKey.Null, HttpStatusCode.OK, "null read with null");
            await CheckRead("missing", PartitionKey.Null, HttpStatusCode.NotFound, "missing read with null");
            await CheckRead("null", PartitionKey.None, HttpStatusCode.NotFound, "null read with undefined");
            explicitNull["id"] = "missing";
            await CheckWrite(explicitNull, "null write with same id as missing");

            async Task CheckWrite(Dictionary<string, object?> item, string operation)
            {
                HttpStatusCode status;
                try
                {
                    status = (await container.CreateItemAsync(item)).StatusCode;
                }
                catch (CosmosException exception)
                {
                    status = exception.StatusCode;
                }
                CheckStatus(operation, HttpStatusCode.Created, status);
            }

            async Task CheckRead(string id, PartitionKey key, HttpStatusCode expected, string operation)
            {
                using var response = await container.ReadItemStreamAsync(id, key);
                CheckStatus(operation, expected, response.StatusCode);
            }

            void CheckStatus(string operation, HttpStatusCode expected, HttpStatusCode actual)
            {
                var result = $"{path}: {operation}: expected {(int)expected}, actual {(int)actual}";
                Console.WriteLine(result);
                if (expected != actual)
                    failures.Add(result);
            }
        }
        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    [TestMethod]
    public async Task Test_StreamPartitionKeyMismatch_IsRejected()
    {
        RequireMissingNullConformance();
        var (_, database, _) = GetSetup();
        var container = await database.CreateContainerAsync("MissingKeyRepro", "/partitionKey", throughput: 400);
        foreach (var upsert in new[] { false, true })
        {
            using var payload = new MemoryStream(Encoding.UTF8.GetBytes("""{"id":"missing-key","name":"sample"}"""));
            using var response = upsert
                ? await container.Container.UpsertItemStreamAsync(payload, PartitionKey.Null)
                : await container.Container.CreateItemStreamAsync(payload, PartitionKey.Null);
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode, "The old explicit-null key must not match a missing field.");
        }
    }

    [TestMethod]
    public Task Test_PartitionKeys_RoundTripAndDelete() =>
        AssertPartitionKeyRoundTrip(new[] { "/partitionKey" }, new[]
        {
            ("{}", PartitionKey.None),
            ("""{"partitionKey":null}""", PartitionKey.Null),
            ("""{"partitionKey":"example"}""", new PartitionKey("example")),
            ("""{"partitionKey":42}""", new PartitionKey(42)),
            ("""{"partitionKey":-12.75}""", new PartitionKey(-12.75)),
            ("""{"partitionKey":true}""", new PartitionKey(true)),
            ("""{"partitionKey":false}""", new PartitionKey(false))
        }, "PartitionKeys", separateNullId: IsVNext);

    [TestMethod]
    public Task Test_NestedPartitionKeys_RoundTripAndDelete() =>
        AssertPartitionKeyRoundTrip(new[] { "/nested/key" }, new[]
        {
            ("""{"nested":{}}""", PartitionKey.None),
            ("""{"nested":{"key":null}}""", PartitionKey.Null),
            ("""{"nested":{"key":42}}""", new PartitionKey(42))
        }, "NestedPartitionKeys", separateNullId: IsVNext);

    [TestMethod]
    public Task Test_HierarchicalPartitionKeys_RoundTripAndDelete() =>
        AssertPartitionKeyRoundTrip(new[] { "/tenant", "/nested/key", "/active" }, new[]
        {
            ("""{"tenant":"one","nested":{"key":42},"active":true}""", new PartitionKeyBuilder().Add("one").Add(42).Add(true).Build()),
            ("""{"tenant":"one","nested":{"key":null},"active":false}""", new PartitionKeyBuilder().Add("one").AddNullValue().Add(false).Build()),
            ("""{"tenant":"one","active":true}""", new PartitionKeyBuilder().Add("one").AddNoneType().Add(true).Build()),
            ("""{"active":false}""", new PartitionKeyBuilder().AddNoneType().AddNoneType().Add(false).Build()),
            ("{}", new PartitionKeyBuilder().AddNoneType().AddNoneType().AddNoneType().Build())
        }, "HierarchicalPartitionKeys");

    private static async Task AssertPartitionKeyRoundTrip(string[] paths, (string json, PartitionKey key)[] cases, string containerName, bool separateNullId = false)
    {
        if (separateNullId)
        {
            Console.WriteLine("[vNext missing/null capability] Only the explicit-null case uses a separate ID to avoid the preview's undefined/null aliasing. All key values, write modes, read checks, and delete modes still execute; other partition keys retain the same ID.");
        }
        var (_, database, connectionString) = GetSetup();
        var configValues = new Dictionary<string, string>
        {
            ["ConnectionString"] = connectionString,
            ["ConnectionMode"] = "Gateway",
            ["AllowBulkExecution"] = "false",
            ["Database"] = database.Id,
            ["Container"] = containerName
        };
        if (paths.Length == 1)
        {
            configValues["PartitionKeyPath"] = paths[0];
        }
        else
        {
            for (int i = 0; i < paths.Length; i++)
                configValues[$"PartitionKeyPaths:{i}"] = paths[i];
            // Container creation and every explicit-key operation must agree on plural precedence.
            configValues["PartitionKeyPath"] = "/ignored";
        }

        var source = new CosmosDataSourceExtension();
        var sink = new CosmosDataSinkExtension();
        var container = database.GetContainer(containerName);
        foreach (var mode in new[] { DataWriteMode.Insert, DataWriteMode.Upsert, DataWriteMode.InsertStream, DataWriteMode.UpsertStream })
        {
            string GetId(int index) => separateNullId && cases[index].key.Equals(PartitionKey.Null)
                ? $"{mode}-null"
                : mode.ToString();

            var items = cases.Select((c, index) =>
            {
                var values = JsonConvert.DeserializeObject<Dictionary<string, object?>>(c.json, RawJsonCosmosSerializer.GetDefaultSettings())!;
                values["id"] = GetId(index);
                return new CosmosDictionaryDataItem(values);
            }).ToArray();
            configValues["WriteMode"] = mode.ToString();
            var config = TestHelpers.CreateConfig(configValues);
            await sink.WriteAsync(items.ToAsyncEnumerable(), config, source, NullLogger.Instance);
            if (mode is DataWriteMode.Upsert or DataWriteMode.UpsertStream)
            {
                foreach (var item in items)
                    item.Items["updated"] = true;
                await sink.WriteAsync(items.ToAsyncEnumerable(), config, source, NullLogger.Instance);
            }

            for (int i = 0; i < cases.Length; i++)
            {
                var result = await container.ReadItemAsync<JObject>(GetId(i), cases[i].key);
                Assert.AreEqual(HttpStatusCode.OK, result.StatusCode, $"{mode}, case {i}");
                var expected = JObject.FromObject(items[i].BuildDynamicObjectTree(requireStringId: true)!);
                foreach (var property in expected.Properties())
                    Assert.IsTrue(JToken.DeepEquals(property.Value, result.Resource[property.Name]), $"{mode}, case {i}, {property.Name}");
                if (paths.Length == 1 && cases[i].key.Equals(PartitionKey.None))
                    Assert.IsFalse(result.Resource.ContainsKey("partitionKey"));
            }

            // Verify deletion by reading the original key, so a mistaken 404-as-success cannot hide a remaining document.
            foreach (var deleteMode in new[] { DataWriteMode.Delete, DataWriteMode.DeleteStream })
            {
                if (deleteMode == DataWriteMode.DeleteStream)
                    await sink.WriteAsync(items.ToAsyncEnumerable(), config, source, NullLogger.Instance);
                configValues["WriteMode"] = deleteMode.ToString();
                await sink.WriteAsync(items.ToAsyncEnumerable(), TestHelpers.CreateConfig(configValues), source, NullLogger.Instance);
                for (int i = 0; i < cases.Length; i++)
                {
                    using var result = await container.ReadItemStreamAsync(GetId(i), cases[i].key);
                    Assert.AreEqual(HttpStatusCode.NotFound, result.StatusCode, $"{mode}, {deleteMode}");
                }
            }
        }
    }


    [TestMethod]
    public async Task Test_Json_RoundTrip_WithLongValues() {
        var (_, database, connectionString) = GetSetup();
        var config = TestHelpers.CreateConfig(new Dictionary<string,string>() {
            { "UseRbacAuth", "false" },
            { "UseDefaultCredentials", "true" },
            { "ConnectionString", connectionString },
            { "ConnectionMode", "Gateway" },
            { "AllowBulkExecution", "false" },
            { "Database", database.Id },
            { "Container", "Test_Json_RoundTrip_WithLongValues" },
            { "RecreateContainer", "true" },
            { "PartitionKeyPath", "/partitionKey" }
        });

        var sourceExtension = new CosmosDataSourceExtension();
        var sinkExtension = new CosmosDataSinkExtension();
        var items = new CosmosDictionaryDataItem[] {
            new CosmosDictionaryDataItem(new Dictionary<string, object?> {
                { "partitionKey", "pk" },
                { "id", "1" },
                { "long", 638676324052177500L }
            })
        };

        await sinkExtension.WriteAsync(items.ToAsyncEnumerable(), config, 
            sourceExtension, NullLogger.Instance);
        
        var fetched = await sourceExtension.ReadAsync(config, NullLogger.Instance).ToArrayAsync();
        Assert.AreEqual(1, fetched.Length);
        Assert.IsInstanceOfType(fetched[0].GetValue("long"), typeof(long));
        Assert.AreEqual(items[0].Items["long"], fetched[0].GetValue("long"));

        var jsonSink = new JsonExtension.JsonFormatWriter();

        var writer = new MemoryStream(500);
        await jsonSink.FormatDataAsync(fetched.ToAsyncEnumerable(), writer, config, NullLogger.Instance);
        var res = Encoding.UTF8.GetString(writer.ToArray());
        Assert.AreEqual("[{\"partitionKey\":\"pk\",\"id\":\"1\",\"long\":638676324052177500}]", res);

        foreach (var preAuthenticate in new[] { false, true })
        {
            await RunSequentialOperationsAsync(connectionString, database.Id,
                config["Container"]!, preAuthenticate);
        }
    }

    private static async Task RunSequentialOperationsAsync(
        string connectionString, string database, string sourceContainer, bool preAuthenticate)
    {
        var source = new CosmosDataSourceExtension();
        var sink = new CosmosDataSinkExtension();
        using var catalog = new CompositionContainer();
        var loader = new Mock<IExtensionLoader>();
        loader.Setup(l => l.BuildExtensionCatalog(It.IsAny<string>())).Returns(catalog);
        loader.Setup(l => l.LoadExtensions<IDataSourceExtension>(catalog)).Returns([source]);
        loader.Setup(l => l.LoadExtensions<IDataSinkExtension>(catalog)).Returns([sink]);
        var values = new Dictionary<string, string?>();
        foreach (var side in new[] { "SourceSettings", "SinkSettings" })
        {
            values[$"{side}:ConnectionString"] = connectionString;
            values[$"{side}:Database"] = database;
            values[$"{side}:ConnectionMode"] = "Gateway";
            values[$"{side}:LimitToEndpoint"] = "true";
            values[$"{side}:UseDefaultCredentials"] = "true";
            values[$"{side}:PreAuthenticate"] = preAuthenticate.ToString();
            values[$"{side}:AllowBulkExecution"] = "false";
        }
        values["SourceSettings:Container"] = sourceContainer;
        values["SinkSettings:PartitionKeyPath"] = "/partitionKey";
        // Reuse two destinations across pre-auth variants to stay within the current emulator's partition limit.
        values["SinkSettings:WriteMode"] = "Upsert";
        for (var i = 0; i < 2; i++)
        {
            values[$"Operations:{i}:SinkSettings:Container"] = $"operation-{i}";
        }
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var command = new RunCommand.CommandHandler(loader.Object, configuration, loggerFactory)
        {
            Source = source.DisplayName,
            Sink = sink.DisplayName,
        };
        var context = new InvocationContext(new RootCommand().Parse(Array.Empty<string>()));
        Assert.AreEqual(0, await command.InvokeAsync(context), "Sequential Cosmos Operations must succeed.");
        for (var i = 0; i < 2; i++)
        {
            var outputConfig = new ConfigurationBuilder()
                .AddConfiguration(configuration.GetSection("SinkSettings"))
                .AddConfiguration(configuration.GetSection($"Operations:{i}:SinkSettings")).Build();
            var output = await source.ReadAsync(outputConfig, NullLogger.Instance).ToArrayAsync();
            Assert.AreEqual(1, output.Length);
            Assert.AreEqual(638676324052177500L, output[0].GetValue("long"));
        }
    }

    [TestMethod]
    public async Task Test_FromJson_WithLongValues() {
        var (_, database, connectionString) = GetSetup();
        var config = TestHelpers.CreateConfig(new Dictionary<string,string>() {
            { "UseRbacAuth", "false" },
            { "ConnectionString", connectionString },
            { "ConnectionMode", "Gateway" },
            { "AllowBulkExecution", "false" },
            { "Database", database.Id },
            { "Container", "Test_FromJson_WithLongValues" },
            { "RecreateContainer", "true" },
            { "PartitionKeyPath", "/partitionKey" },
            { "FilePath", "Data/LongValue.json" }
        });

        var jsonSource = new JsonExtension.JsonFormatReader();
        var cosmosSink = new CosmosDataSinkExtension();
        var cosmosSource = new CosmosDataSourceExtension();

        var fileSource = new Common.FileDataSource();

        await cosmosSink.WriteAsync(jsonSource.ParseDataAsync(fileSource, config, NullLogger.Instance),
            config, new JsonExtension.JsonFileSource(), NullLogger.Instance);

        var fetched = await cosmosSource.ReadAsync(config, NullLogger.Instance).ToArrayAsync();
        Assert.IsInstanceOfType(fetched[0].GetValue("long"), typeof(long));
        Assert.AreEqual(638676324052177500, fetched[0].GetValue("long"));
    }
}
