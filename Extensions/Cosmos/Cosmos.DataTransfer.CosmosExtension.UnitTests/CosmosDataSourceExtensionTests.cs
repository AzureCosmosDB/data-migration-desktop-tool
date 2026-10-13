using System.Text;
using Cosmos.DataTransfer.Common.UnitTests;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Azure.Cosmos;
using System.Net;

namespace Cosmos.DataTransfer.CosmosExtension.UnitTests;

[TestClass]
public class CosmosDataSourceExtensionTests
{
    private static Database? testDatabase;
    private static CosmosClient? cosmosClient;
    private static string? connectionString;

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
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
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
    public async Task Test_Json_RoundTrip_WithLongValues() {
        var (_, database, connectionString) = GetSetup();
        var config = TestHelpers.CreateConfig(new Dictionary<string,string>() {
            { "UseRbacAuth", "false" },
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
