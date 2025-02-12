using System.Text;
using Cosmos.DataTransfer.Common.UnitTests;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Azure.Cosmos;
using System.Net;

namespace Cosmos.DataTransfer.CosmosExtension.UnitTests;

[TestClass]
public class CosmosDataSourceExtensionTests : IDisposable
{
    private static readonly Database testDatabase;
    private static readonly CosmosClient cosmosClient;
    private static readonly string connectionString;
    private static readonly Action dismantleTestDb = () => {};
    private static readonly bool runTests = false;

    // Sets up a connection to CosmosDB. 
    // Default values here are to a local Cosmos DB emulator.
    // Use the two environment variables to pass a custom connection.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    static CosmosDataSourceExtensionTests() {
        string? endpoint = Environment.GetEnvironmentVariable("Cosmos_Endpoint");
        string? accountKey = Environment.GetEnvironmentVariable("Cosmos_Key");

        if (endpoint is null || accountKey is null) {
            Console.WriteLine("Connection details for Cosmos DB not found. Ignoring tests...");
            return;
        }
        
        connectionString = $"AccountEndpoint={endpoint};AccountKey={accountKey};";

        var fullname = typeof(CosmosDataSourceExtensionTests).Assembly.ManifestModule.Name;
        var dbname = typeof(CosmosDataSourceExtensionTests).Name + "-" + Guid.NewGuid().ToString().Substring(0, 8);

        CosmosClient client = new(
            accountEndpoint: endpoint,
            authKeyOrResourceToken: accountKey,
            new CosmosClientOptions {
                ApplicationName = fullname
            }
        );
        cosmosClient = client;
        
        DatabaseResponse db;
        try {
            _ = client.ReadAccountAsync().Result;
            db = client.CreateDatabaseAsync(dbname, (int?)null, new RequestOptions()).Result;
        } catch (Exception) {
            Console.WriteLine("Issues encountered connecting to Cosmos DB. Ignoring these tests...");
            return;
        }
        testDatabase = db.Database;
        runTests = true;
        dismantleTestDb = () => {
            
        };
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    [ClassCleanup]
    public static void CleanUp() {
        if (runTests) {
            try {
                testDatabase.DeleteAsync().Wait();
            } catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound) {
                // Ignore; the database has already been deleted.
            } catch (AggregateException e) when (e.InnerException is CosmosException ce 
                && ce.StatusCode == HttpStatusCode.NotFound)
            {
                // Ignore; the database has already been deleted.
            }
        }
    }

    public void Dispose()
    {
        CleanUp();
    }

    private static (CosmosClient client, Database database, string connectionString) GetSetup() {
        if (!runTests) {
            throw new AssertInconclusiveException("Cosmos DB not active");
        }
        return (cosmosClient, testDatabase, connectionString);
    }


    [TestMethod]
    public async Task Test_Json_RoundTrip_WithLongValues() {
        var (_, database, connectionString) = GetSetup();
        var config = TestHelpers.CreateConfig(new Dictionary<string,string>() {
            { "UseRbacAuth", "false" },
            { "ConnectionString", connectionString },
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
