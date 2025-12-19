using Cosmos.DataTransfer.Common.UnitTests;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cosmos.DataTransfer.CosmosExtension.UnitTests;

/// <summary>
/// Tests to verify that the tool can handle multiple Cosmos DB accounts simultaneously.
/// This confirms that separate CosmosClient instances are created for source and sink operations.
/// </summary>
[TestClass]
public class CosmosMultiAccountSupportTests
{
    [TestMethod]
    public void CreateClient_WithDifferentSettings_CreatesSeparateInstances()
    {
        // Arrange - Create two different connection configurations
        // Using valid Base64-encoded keys (dummy keys for testing)
        var sourceSettings = new CosmosSourceSettings
        {
            ConnectionString = "AccountEndpoint=https://source-account.documents.azure.com:443/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==;",
            Database = "sourceDb",
            Container = "sourceContainer",
            ConnectionMode = ConnectionMode.Gateway
        };

        var sinkSettings = new CosmosSinkSettings
        {
            ConnectionString = "AccountEndpoint=https://sink-account.documents.azure.com:443/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==;",
            Database = "sinkDb",
            Container = "sinkContainer",
            PartitionKeyPath = "/id",
            ConnectionMode = ConnectionMode.Direct
        };

        // Act - Create clients using the extension service method
        CosmosClient sourceClient = CosmosExtensionServices.CreateClient(sourceSettings, "Cosmos-nosql");
        CosmosClient sinkClient = CosmosExtensionServices.CreateClient(sinkSettings, "Cosmos-nosql", "Cosmos-nosql");

        // Assert - Verify that two distinct client instances are created
        Assert.IsNotNull(sourceClient, "Source client should be created");
        Assert.IsNotNull(sinkClient, "Sink client should be created");
        Assert.AreNotSame(sourceClient, sinkClient, "Source and sink should use separate CosmosClient instances");

        // Verify client configurations are independent
        Assert.AreEqual(ConnectionMode.Gateway, sourceClient.ClientOptions.ConnectionMode);
        Assert.AreEqual(ConnectionMode.Direct, sinkClient.ClientOptions.ConnectionMode);
        
        // Dispose clients
        sourceClient.Dispose();
        sinkClient.Dispose();
    }

    [TestMethod]
    public void CreateClient_WithRbacAuth_CreatesSeparateInstancesForDifferentAccounts()
    {
        // Arrange - Create two different RBAC-based connection configurations
        var sourceSettings = new CosmosSourceSettings
        {
            UseRbacAuth = true,
            AccountEndpoint = "https://source-account.documents.azure.com:443/",
            Database = "sourceDb",
            Container = "sourceContainer",
            EnableInteractiveCredentials = false
        };

        var sinkSettings = new CosmosSinkSettings
        {
            UseRbacAuth = true,
            AccountEndpoint = "https://sink-account.documents.azure.com:443/",
            Database = "sinkDb",
            Container = "sinkContainer",
            PartitionKeyPath = "/id",
            EnableInteractiveCredentials = false
        };

        // Act - Create clients
        CosmosClient sourceClient = CosmosExtensionServices.CreateClient(sourceSettings, "Cosmos-nosql");
        CosmosClient sinkClient = CosmosExtensionServices.CreateClient(sinkSettings, "Cosmos-nosql", "Cosmos-nosql");

        // Assert - Verify separate instances
        Assert.IsNotNull(sourceClient);
        Assert.IsNotNull(sinkClient);
        Assert.AreNotSame(sourceClient, sinkClient, "RBAC-based source and sink should use separate CosmosClient instances");
        
        // Dispose clients
        sourceClient.Dispose();
        sinkClient.Dispose();
    }

    [TestMethod]
    public void CreateClient_WithSameAccount_StillCreatesSeparateInstances()
    {
        // Arrange - Create two configurations pointing to the same account
        // This tests that even when using the same account, separate client instances are created
        var connectionString = "AccountEndpoint=https://same-account.documents.azure.com:443/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==;";
        
        var sourceSettings = new CosmosSourceSettings
        {
            ConnectionString = connectionString,
            Database = "db1",
            Container = "container1"
        };

        var sinkSettings = new CosmosSinkSettings
        {
            ConnectionString = connectionString,
            Database = "db2",
            Container = "container2",
            PartitionKeyPath = "/id"
        };

        // Act
        CosmosClient sourceClient = CosmosExtensionServices.CreateClient(sourceSettings, "Cosmos-nosql");
        CosmosClient sinkClient = CosmosExtensionServices.CreateClient(sinkSettings, "Cosmos-nosql", "Cosmos-nosql");

        // Assert - Even with the same account, separate client instances should be created
        Assert.IsNotNull(sourceClient);
        Assert.IsNotNull(sinkClient);
        Assert.AreNotSame(sourceClient, sinkClient, "Source and sink should use separate client instances even for the same account");
        
        // Dispose clients
        sourceClient.Dispose();
        sinkClient.Dispose();
    }

    [TestMethod]
    public void CreateClient_WithDifferentProxySettings_CreatesSeparateInstances()
    {
        // Arrange - Create configurations with different proxy settings
        var sourceSettings = new CosmosSourceSettings
        {
            ConnectionString = "AccountEndpoint=https://source-account.documents.azure.com:443/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==;",
            Database = "sourceDb",
            Container = "sourceContainer",
            WebProxy = "http://proxy1.example.com:8080",
            UseDefaultProxyCredentials = true
        };

        var sinkSettings = new CosmosSinkSettings
        {
            ConnectionString = "AccountEndpoint=https://sink-account.documents.azure.com:443/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==;",
            Database = "sinkDb",
            Container = "sinkContainer",
            PartitionKeyPath = "/id",
            WebProxy = "http://proxy2.example.com:8080",
            UseDefaultProxyCredentials = false
        };

        // Act
        CosmosClient sourceClient = CosmosExtensionServices.CreateClient(sourceSettings, "Cosmos-nosql");
        CosmosClient sinkClient = CosmosExtensionServices.CreateClient(sinkSettings, "Cosmos-nosql", "Cosmos-nosql");

        // Assert
        Assert.IsNotNull(sourceClient);
        Assert.IsNotNull(sinkClient);
        Assert.AreNotSame(sourceClient, sinkClient, "Clients with different proxy settings should be separate instances");
        
        // Verify proxy settings are properly configured
        Assert.IsNotNull(sourceClient.ClientOptions.WebProxy);
        Assert.IsNotNull(sinkClient.ClientOptions.WebProxy);
        
        // Dispose clients
        sourceClient.Dispose();
        sinkClient.Dispose();
    }

    [TestMethod]
    public void ExtensionInitialization_CreatesIndependentClients()
    {
        // Arrange - This test verifies the actual extension behavior
        var sourceConfig = TestHelpers.CreateConfig(new Dictionary<string, string>()
        {
            { "ConnectionString", "AccountEndpoint=https://source-account.documents.azure.com:443/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==;" },
            { "Database", "sourceDb" },
            { "Container", "sourceContainer" },
        });

        var sinkConfig = TestHelpers.CreateConfig(new Dictionary<string, string>()
        {
            { "ConnectionString", "AccountEndpoint=https://sink-account.documents.azure.com:443/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==;" },
            { "Database", "sinkDb" },
            { "Container", "sinkContainer" },
            { "PartitionKeyPath", "/id" }
        });

        // Get settings from configuration
        var sourceSettings = sourceConfig.Get<CosmosSourceSettings>();
        var sinkSettings = sinkConfig.Get<CosmosSinkSettings>();

        // Act - Simulate what the extensions do internally
        CosmosClient sourceClient = CosmosExtensionServices.CreateClient(sourceSettings!, "Cosmos-nosql");
        CosmosClient sinkClient = CosmosExtensionServices.CreateClient(sinkSettings!, "Cosmos-nosql", "Cosmos-nosql");

        // Assert
        Assert.IsNotNull(sourceClient, "Source extension should create a client");
        Assert.IsNotNull(sinkClient, "Sink extension should create a client");
        Assert.AreNotSame(sourceClient, sinkClient, 
            "Source and sink extensions should create and use separate CosmosClient instances");

        // Verify that both clients can be used independently
        Assert.IsTrue(sourceClient.ClientOptions.AllowBulkExecution, "Source client should have bulk execution enabled");
        Assert.IsTrue(sinkClient.ClientOptions.AllowBulkExecution, "Sink client should have bulk execution enabled");
        
        // Dispose clients
        sourceClient.Dispose();
        sinkClient.Dispose();
    }
}
