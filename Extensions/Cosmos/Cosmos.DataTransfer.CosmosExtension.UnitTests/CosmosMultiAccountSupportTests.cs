using Cosmos.DataTransfer.Common.UnitTests;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cosmos.DataTransfer.CosmosExtension.UnitTests;

/// <summary>
/// Tests to verify that the tool can handle multiple Cosmos DB sink accounts simultaneously.
/// This confirms that separate CosmosClient instances are created for multiple sink operations
/// to different Cosmos DB accounts.
/// </summary>
[TestClass]
public class CosmosMultiAccountSupportTests
{
    [TestMethod]
    public void CreateClient_WithTwoDifferentSinkAccounts_CreatesSeparateInstances()
    {
        // Arrange - Create two different sink configurations for different Cosmos DB accounts
        // This simulates writing to two different accounts simultaneously (e.g., in multiple operations)
        // Using valid Base64-encoded keys (dummy keys for testing)
        var sink1Settings = new CosmosSinkSettings
        {
            ConnectionString = "AccountEndpoint=https://sink1-account.documents.azure.com:443/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==;",
            Database = "sink1Db",
            Container = "sink1Container",
            PartitionKeyPath = "/id",
            ConnectionMode = ConnectionMode.Gateway
        };

        var sink2Settings = new CosmosSinkSettings
        {
            ConnectionString = "AccountEndpoint=https://sink2-account.documents.azure.com:443/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==;",
            Database = "sink2Db",
            Container = "sink2Container",
            PartitionKeyPath = "/id",
            ConnectionMode = ConnectionMode.Direct
        };

        // Act - Create clients for two different sink accounts
        CosmosClient sink1Client = CosmosExtensionServices.CreateClient(sink1Settings, "Cosmos-nosql", "JSON");
        CosmosClient sink2Client = CosmosExtensionServices.CreateClient(sink2Settings, "Cosmos-nosql", "JSON");

        // Assert - Verify that two distinct client instances are created for different sink accounts
        Assert.IsNotNull(sink1Client, "First sink client should be created");
        Assert.IsNotNull(sink2Client, "Second sink client should be created");
        Assert.AreNotSame(sink1Client, sink2Client, "Two different sink accounts should use separate CosmosClient instances");

        // Verify client configurations are independent
        Assert.AreEqual(ConnectionMode.Gateway, sink1Client.ClientOptions.ConnectionMode);
        Assert.AreEqual(ConnectionMode.Direct, sink2Client.ClientOptions.ConnectionMode);
        
        // Dispose clients
        sink1Client.Dispose();
        sink2Client.Dispose();
    }

    [TestMethod]
    public void CreateClient_WithRbacAuth_CreatesSeparateInstancesForTwoDifferentSinkAccounts()
    {
        // Arrange - Create two different RBAC-based sink configurations for different accounts
        var sink1Settings = new CosmosSinkSettings
        {
            UseRbacAuth = true,
            AccountEndpoint = "https://sink1-account.documents.azure.com:443/",
            Database = "sink1Db",
            Container = "sink1Container",
            PartitionKeyPath = "/id",
            EnableInteractiveCredentials = false
        };

        var sink2Settings = new CosmosSinkSettings
        {
            UseRbacAuth = true,
            AccountEndpoint = "https://sink2-account.documents.azure.com:443/",
            Database = "sink2Db",
            Container = "sink2Container",
            PartitionKeyPath = "/id",
            EnableInteractiveCredentials = false
        };

        // Act - Create clients for two different sink accounts
        CosmosClient sink1Client = CosmosExtensionServices.CreateClient(sink1Settings, "Cosmos-nosql", "JSON");
        CosmosClient sink2Client = CosmosExtensionServices.CreateClient(sink2Settings, "Cosmos-nosql", "JSON");

        // Assert - Verify separate instances for different sink accounts
        Assert.IsNotNull(sink1Client);
        Assert.IsNotNull(sink2Client);
        Assert.AreNotSame(sink1Client, sink2Client, "RBAC-based sinks to different accounts should use separate CosmosClient instances");
        
        // Dispose clients
        sink1Client.Dispose();
        sink2Client.Dispose();
    }

    [TestMethod]
    public void CreateClient_WithTwoSinksToSameAccount_StillCreatesSeparateInstances()
    {
        // Arrange - Create two sink configurations pointing to the same account
        // This tests that even when using the same account for multiple sinks, separate client instances are created
        var connectionString = "AccountEndpoint=https://same-account.documents.azure.com:443/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==;";
        
        var sink1Settings = new CosmosSinkSettings
        {
            ConnectionString = connectionString,
            Database = "db1",
            Container = "container1",
            PartitionKeyPath = "/id"
        };

        var sink2Settings = new CosmosSinkSettings
        {
            ConnectionString = connectionString,
            Database = "db2",
            Container = "container2",
            PartitionKeyPath = "/id"
        };

        // Act - Create clients for two sinks to the same account
        CosmosClient sink1Client = CosmosExtensionServices.CreateClient(sink1Settings, "Cosmos-nosql", "JSON");
        CosmosClient sink2Client = CosmosExtensionServices.CreateClient(sink2Settings, "Cosmos-nosql", "JSON");

        // Assert - Even with the same account, separate client instances should be created for multiple sinks
        Assert.IsNotNull(sink1Client);
        Assert.IsNotNull(sink2Client);
        Assert.AreNotSame(sink1Client, sink2Client, "Multiple sinks should use separate client instances even for the same account");
        
        // Dispose clients
        sink1Client.Dispose();
        sink2Client.Dispose();
    }

    [TestMethod]
    public void CreateClient_WithTwoSinksWithDifferentProxySettings_CreatesSeparateInstances()
    {
        // Arrange - Create two sink configurations with different proxy settings
        var sink1Settings = new CosmosSinkSettings
        {
            ConnectionString = "AccountEndpoint=https://sink1-account.documents.azure.com:443/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==;",
            Database = "sink1Db",
            Container = "sink1Container",
            PartitionKeyPath = "/id",
            WebProxy = "http://proxy1.example.com:8080",
            UseDefaultProxyCredentials = true
        };

        var sink2Settings = new CosmosSinkSettings
        {
            ConnectionString = "AccountEndpoint=https://sink2-account.documents.azure.com:443/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==;",
            Database = "sink2Db",
            Container = "sink2Container",
            PartitionKeyPath = "/id",
            WebProxy = "http://proxy2.example.com:8080",
            UseDefaultProxyCredentials = false
        };

        // Act - Create clients for two sinks with different proxy settings
        CosmosClient sink1Client = CosmosExtensionServices.CreateClient(sink1Settings, "Cosmos-nosql", "JSON");
        CosmosClient sink2Client = CosmosExtensionServices.CreateClient(sink2Settings, "Cosmos-nosql", "JSON");

        // Assert - Verify separate instances for different proxy configurations
        Assert.IsNotNull(sink1Client);
        Assert.IsNotNull(sink2Client);
        Assert.AreNotSame(sink1Client, sink2Client, "Multiple sinks with different proxy settings should be separate instances");
        
        // Verify proxy settings are properly configured
        Assert.IsNotNull(sink1Client.ClientOptions.WebProxy);
        Assert.IsNotNull(sink2Client.ClientOptions.WebProxy);
        
        // Dispose clients
        sink1Client.Dispose();
        sink2Client.Dispose();
    }

    [TestMethod]
    public void SinkExtensionInitialization_CreatesIndependentClientsForMultipleSinks()
    {
        // Arrange - This test verifies that multiple sink operations can use independent clients
        var sink1Config = TestHelpers.CreateConfig(new Dictionary<string, string>()
        {
            { "ConnectionString", "AccountEndpoint=https://sink1-account.documents.azure.com:443/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==;" },
            { "Database", "sink1Db" },
            { "Container", "sink1Container" },
            { "PartitionKeyPath", "/id" }
        });

        var sink2Config = TestHelpers.CreateConfig(new Dictionary<string, string>()
        {
            { "ConnectionString", "AccountEndpoint=https://sink2-account.documents.azure.com:443/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==;" },
            { "Database", "sink2Db" },
            { "Container", "sink2Container" },
            { "PartitionKeyPath", "/id" }
        });

        // Get settings from configuration
        var sink1Settings = sink1Config.Get<CosmosSinkSettings>();
        var sink2Settings = sink2Config.Get<CosmosSinkSettings>();

        // Act - Simulate what multiple sink operations do internally
        CosmosClient sink1Client = CosmosExtensionServices.CreateClient(sink1Settings!, "Cosmos-nosql", "JSON");
        CosmosClient sink2Client = CosmosExtensionServices.CreateClient(sink2Settings!, "Cosmos-nosql", "JSON");

        // Assert - Verify that multiple sink operations create independent clients
        Assert.IsNotNull(sink1Client, "First sink extension should create a client");
        Assert.IsNotNull(sink2Client, "Second sink extension should create a client");
        Assert.AreNotSame(sink1Client, sink2Client, 
            "Multiple sink operations should create and use separate CosmosClient instances");

        // Verify that both clients can be used independently
        Assert.IsTrue(sink1Client.ClientOptions.AllowBulkExecution, "First sink client should have bulk execution enabled");
        Assert.IsTrue(sink2Client.ClientOptions.AllowBulkExecution, "Second sink client should have bulk execution enabled");
        
        // Dispose clients
        sink1Client.Dispose();
        sink2Client.Dispose();
    }
}
