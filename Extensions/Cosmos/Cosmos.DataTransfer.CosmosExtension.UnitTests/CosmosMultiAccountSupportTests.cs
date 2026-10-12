using Cosmos.DataTransfer.Common.UnitTests;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cosmos.DataTransfer.CosmosExtension.UnitTests;

/// <summary>
/// Verifies client and configuration isolation without connecting to live accounts.
/// Operation ordering and source rereads are covered by RunCommandTests.
/// </summary>
[TestClass]
public class CosmosMultiAccountSupportTests
{
    [TestMethod]
    public void CreateClient_WithTwoDifferentSinkAccounts_CreatesSeparateInstances()
    {
        // Arrange - Create two different sink configurations for different Cosmos DB accounts
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
        using CosmosClient sink1Client = CosmosExtensionServices.CreateClient(sink1Settings, "Cosmos-nosql", NullLogger.Instance, "JSON");
        using CosmosClient sink2Client = CosmosExtensionServices.CreateClient(sink2Settings, "Cosmos-nosql", NullLogger.Instance, "JSON");

        // Assert - Verify that two distinct client instances are created for different sink accounts
        Assert.IsNotNull(sink1Client, "First sink client should be created");
        Assert.IsNotNull(sink2Client, "Second sink client should be created");
        Assert.AreNotSame(sink1Client, sink2Client, "Two different sink accounts should use separate CosmosClient instances");

        // Verify client configurations are independent
        Assert.AreEqual(ConnectionMode.Gateway, sink1Client.ClientOptions.ConnectionMode);
        Assert.AreEqual(ConnectionMode.Direct, sink2Client.ClientOptions.ConnectionMode);
        Assert.AreEqual(new Uri("https://sink1-account.documents.azure.com:443/"), sink1Client.Endpoint);
        Assert.AreEqual(new Uri("https://sink2-account.documents.azure.com:443/"), sink2Client.Endpoint);
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
        using CosmosClient sink1Client = CosmosExtensionServices.CreateClient(sink1Settings, "Cosmos-nosql", NullLogger.Instance, "JSON");
        using CosmosClient sink2Client = CosmosExtensionServices.CreateClient(sink2Settings, "Cosmos-nosql", NullLogger.Instance, "JSON");

        // Assert - Verify separate instances for different sink accounts
        Assert.IsNotNull(sink1Client);
        Assert.IsNotNull(sink2Client);
        Assert.AreNotSame(sink1Client, sink2Client, "RBAC-based sinks to different accounts should use separate CosmosClient instances");
        Assert.AreEqual(new Uri(sink1Settings.AccountEndpoint), sink1Client.Endpoint);
        Assert.AreEqual(new Uri(sink2Settings.AccountEndpoint), sink2Client.Endpoint);
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
        using CosmosClient sink1Client = CosmosExtensionServices.CreateClient(sink1Settings, "Cosmos-nosql", NullLogger.Instance, "JSON");
        using CosmosClient sink2Client = CosmosExtensionServices.CreateClient(sink2Settings, "Cosmos-nosql", NullLogger.Instance, "JSON");

        // Assert - Even with the same account, separate client instances should be created for multiple sinks
        Assert.IsNotNull(sink1Client);
        Assert.IsNotNull(sink2Client);
        Assert.AreNotSame(sink1Client, sink2Client, "Multiple sinks should use separate client instances even for the same account");
        Assert.AreEqual(sink1Client.Endpoint, sink2Client.Endpoint);
        Assert.IsFalse(sink1Client.ClientOptions.AllowBulkExecution);
        Assert.IsFalse(sink2Client.ClientOptions.AllowBulkExecution);
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
        using CosmosClient sink1Client = CosmosExtensionServices.CreateClient(sink1Settings, "Cosmos-nosql", NullLogger.Instance, "JSON");
        using CosmosClient sink2Client = CosmosExtensionServices.CreateClient(sink2Settings, "Cosmos-nosql", NullLogger.Instance, "JSON");

        // Assert - Verify separate instances for different proxy configurations
        Assert.IsNotNull(sink1Client);
        Assert.IsNotNull(sink2Client);
        Assert.AreNotSame(sink1Client, sink2Client, "Multiple sinks with different proxy settings should be separate instances");
        
        // Verify proxy settings are properly configured
        Assert.IsNotNull(sink1Client.ClientOptions.WebProxy);
        Assert.IsNotNull(sink2Client.ClientOptions.WebProxy);
        Assert.AreNotSame(sink1Client.ClientOptions.WebProxy, sink2Client.ClientOptions.WebProxy);
        Assert.AreEqual(new Uri(sink1Settings.WebProxy), sink1Client.ClientOptions.WebProxy.GetProxy(sink1Client.Endpoint));
        Assert.AreEqual(new Uri(sink2Settings.WebProxy), sink2Client.ClientOptions.WebProxy.GetProxy(sink2Client.Endpoint));
        Assert.IsNotNull(sink1Client.ClientOptions.WebProxy.Credentials);
        Assert.IsNull(sink2Client.ClientOptions.WebProxy.Credentials);
    }

    [DataTestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void CreateClient_FromSeparateSinkConfigurations_PreservesIndependentOptions(bool sink1Bulk, bool sink2Bulk)
    {
        // Arrange - This test verifies that multiple sink operations can use independent clients
        var sink1Config = TestHelpers.CreateConfig(new Dictionary<string, string>()
        {
            { "ConnectionString", "AccountEndpoint=https://sink1-account.documents.azure.com:443/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==;" },
            { "Database", "sink1Db" },
            { "Container", "sink1Container" },
            { "PartitionKeyPath", "/id" },
            { "AllowBulkExecution", sink1Bulk.ToString() }
        });

        var sink2Config = TestHelpers.CreateConfig(new Dictionary<string, string>()
        {
            { "ConnectionString", "AccountEndpoint=https://sink2-account.documents.azure.com:443/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==;" },
            { "Database", "sink2Db" },
            { "Container", "sink2Container" },
            { "PartitionKeyPath", "/id" },
            { "AllowBulkExecution", sink2Bulk.ToString() }
        });

        // Get settings from configuration
        var sink1Settings = sink1Config.Get<CosmosSinkSettings>();
        var sink2Settings = sink2Config.Get<CosmosSinkSettings>();
        Assert.IsNotNull(sink1Settings);
        Assert.IsNotNull(sink2Settings);

        // Act - Simulate what multiple sink operations do internally
        using CosmosClient sink1Client = CosmosExtensionServices.CreateClient(sink1Settings, "Cosmos-nosql", NullLogger.Instance, "JSON");
        using CosmosClient sink2Client = CosmosExtensionServices.CreateClient(sink2Settings, "Cosmos-nosql", NullLogger.Instance, "JSON");

        // Assert - Verify that multiple sink operations create independent clients
        Assert.IsNotNull(sink1Client, "First sink extension should create a client");
        Assert.IsNotNull(sink2Client, "Second sink extension should create a client");
        Assert.AreNotSame(sink1Client, sink2Client, 
            "Multiple sink operations should create and use separate CosmosClient instances");

        // Verify that both clients can be used independently
        Assert.AreEqual(sink1Bulk, sink1Client.ClientOptions.AllowBulkExecution);
        Assert.AreEqual(sink2Bulk, sink2Client.ClientOptions.AllowBulkExecution);
        Assert.AreEqual(new Uri("https://sink1-account.documents.azure.com:443/"), sink1Client.Endpoint);
        Assert.AreEqual(new Uri("https://sink2-account.documents.azure.com:443/"), sink2Client.Endpoint);
    }

    [TestMethod]
    public void CreateClient_WithConnectionStringAndRbac_CreatesIndependentClients()
    {
        var keySettings = new CosmosSinkSettings
        {
            ConnectionString = "AccountEndpoint=https://key-account.documents.azure.com:443/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==;",
            Database = "keyDb",
            Container = "keyContainer",
            PartitionKeyPath = "/id"
        };
        var rbacSettings = new CosmosSinkSettings
        {
            UseRbacAuth = true,
            AccountEndpoint = "https://rbac-account.documents.azure.com:443/",
            Database = "rbacDb",
            Container = "rbacContainer",
            PartitionKeyPath = "/id"
        };

        using var keyClient = CosmosExtensionServices.CreateClient(keySettings, "Cosmos-nosql", NullLogger.Instance, "JSON");
        using var rbacClient = CosmosExtensionServices.CreateClient(rbacSettings, "Cosmos-nosql", NullLogger.Instance, "JSON");

        Assert.AreNotSame(keyClient, rbacClient);
        Assert.AreEqual(new Uri("https://key-account.documents.azure.com:443/"), keyClient.Endpoint);
        Assert.AreEqual(new Uri(rbacSettings.AccountEndpoint), rbacClient.Endpoint);
    }
}
