using Azure.Identity;
using Microsoft.Extensions.Logging;
using Moq;

namespace Cosmos.DataTransfer.CosmosExtension.UnitTests;

[TestClass]
public class CosmosExtensionServicesCredentialTests
{
    [TestMethod]
    public void GetTokenCredentialSelection_WithNoServicePrincipalInfo_ReturnsDefaultCredential()
    {
        var settings = new CosmosSourceSettings
        {
            UseRbacAuth = true,
            AccountEndpoint = "https://localhost:8081/",
            Database = "db",
            Container = "container",
        };

        var selection = CosmosExtensionServices.GetTokenCredentialSelection(settings);

        Assert.AreEqual(CosmosExtensionServices.TokenCredentialSelection.DefaultAzureCredential, selection);
    }

    [TestMethod]
    public void GetTokenCredentialSelection_WithTenantClientAndSecret_ReturnsClientSecretCredential()
    {
        var settings = new CosmosSourceSettings
        {
            UseRbacAuth = true,
            AccountEndpoint = "https://localhost:8081/",
            Database = "db",
            Container = "container",
            TenantId = "tenant-id",
            ClientId = "client-id",
            ClientSecret = "client-secret",
        };

        var selection = CosmosExtensionServices.GetTokenCredentialSelection(settings);

        Assert.AreEqual(CosmosExtensionServices.TokenCredentialSelection.ClientSecretCredential, selection);
    }

    [TestMethod]
    public void GetTokenCredentialSelection_WithTenantClientAndCertificatePath_ReturnsClientCertificateCredential()
    {
        var settings = new CosmosSourceSettings
        {
            UseRbacAuth = true,
            AccountEndpoint = "https://localhost:8081/",
            Database = "db",
            Container = "container",
            TenantId = "tenant-id",
            ClientId = "client-id",
            ClientCertificatePath = "./certs/cert.pfx",
        };

        var selection = CosmosExtensionServices.GetTokenCredentialSelection(settings);

        Assert.AreEqual(CosmosExtensionServices.TokenCredentialSelection.ClientCertificateCredential, selection);
    }

    [TestMethod]
    public void CreateRbacTokenCredential_WithNoServicePrincipalInfo_ReturnsDefaultAzureCredential()
    {
        var loggerMock = new Mock<ILogger>();
        var settings = new CosmosSourceSettings
        {
            UseRbacAuth = true,
            AccountEndpoint = "https://localhost:8081/",
            Database = "db",
            Container = "container",
        };

        var credential = CosmosExtensionServices.CreateRbacTokenCredential(settings, loggerMock.Object);

        Assert.IsInstanceOfType<DefaultAzureCredential>(credential);
    }

    [TestMethod]
    public void CreateRbacTokenCredential_WithInvalidCertificatePath_ThrowsFriendlyConfigurationError()
    {
        var loggerMock = new Mock<ILogger>();
        var settings = new CosmosSourceSettings
        {
            UseRbacAuth = true,
            AccountEndpoint = "https://localhost:8081/",
            Database = "db",
            Container = "container",
            TenantId = "tenant-id",
            ClientId = "client-id",
            ClientCertificatePath = "./certs/does-not-exist.pfx",
        };

        var ex = Assert.ThrowsException<InvalidOperationException>(() =>
            CosmosExtensionServices.CreateRbacTokenCredential(settings, loggerMock.Object));

        StringAssert.Contains(ex.Message, "Failed to configure RBAC credentials");
        Assert.IsNotNull(ex.InnerException);
    }
}
