using Cosmos.DataTransfer.Interfaces;

namespace Cosmos.DataTransfer.CosmosExtension.UnitTests;

[TestClass]
public class CosmosSourceSettingsTests
{
    [TestMethod]
    public void GetValidationErrors_WithNoConnection_ReturnsError()
    {
        var settings = new CosmosSourceSettings
        {
            Database = "db",
            Container = "container",
        };

        var validationErrors = settings.GetValidationErrors();

        Assert.AreEqual(1, validationErrors.Count(v => v.Contains(nameof(CosmosSinkSettings.ConnectionString))));
    }

    [TestMethod]
    public void GetValidationErrors_WithNoRbacConnection_ReturnsError()
    {
        var settings = new CosmosSourceSettings
        {
            UseRbacAuth = true,
            Database = "db",
            Container = "container",
        };

        var validationErrors = settings.GetValidationErrors();

        Assert.AreEqual(1, validationErrors.Count(v => v.Contains(nameof(CosmosSinkSettings.AccountEndpoint))));
    }

    [TestMethod]
    public void GetValidationErrors_WithRbacAuthAndIncompleteServicePrincipalInfo_ReturnsErrors()
    {
        var settings = new CosmosSinkSettings
        {
            UseRbacAuth = true,
            Database = "db",
            Container = "container",
            AccountEndpoint = "https://example.documents.azure.com:443/",
            TenantId = "tenant-id"
        };

        var validationErrors = settings.GetValidationErrors();
        
        Assert.AreEqual(1, validationErrors.Count(v => v.Contains(nameof(CosmosSinkSettings.TenantId)) && v.Contains(nameof(CosmosSinkSettings.ClientId))));
    }

    [TestMethod]
    public void GetValidationErrors_WithRbacAuthAndServicePrincipalButNoSecretOrCertificateInfo_ReturnsErrors()
    {
        var settings = new CosmosSinkSettings
        {
            UseRbacAuth = true,
            Database = "db",
            Container = "container",
            AccountEndpoint = "https://example.documents.azure.com:443/",
            TenantId = "tenant-id",
            ClientId = "client-id",
        };

        var validationErrors = settings.GetValidationErrors();
        
        Assert.AreEqual(1, validationErrors.Count(v => v.Contains(nameof(CosmosSinkSettings.ClientSecret)) && v.Contains(nameof(CosmosSinkSettings.ClientCertificatePath))));
    }

    [TestMethod]
    public void GetValidationErrors_WithRbacAuthAndServicePrincipalClientSecretInfo_ReturnsNoErrors()
    {
        var settings = new CosmosSinkSettings
        {
            UseRbacAuth = true,
            Database = "db",
            Container = "container",
            AccountEndpoint = "https://localhost:8081/",
            TenantId = "tenant-id",
            ClientId = "client-id",
            ClientSecret = "client-secret",
        };
        var validationErrors = settings.GetValidationErrors();
        
        Assert.IsFalse(validationErrors.Any());
    }

    [TestMethod]
    public void GetValidationErrors_WithRbacAuthAndServicePrincipalClientCertificateInfo_ReturnsNoErrors()
    {
        var settings = new CosmosSinkSettings
        {
            UseRbacAuth = true,
            Database = "db",
            Container = "container",
            AccountEndpoint = "https://localhost:8081/",
            TenantId = "tenant-id",
            ClientId = "client-id",
            ClientCertificatePath = "./certs/cert.pfx",
        };
        var validationErrors = settings.GetValidationErrors();
        
        Assert.IsFalse(validationErrors.Any());
    }

    [TestMethod]
    public void Validate_WithConnectionString_Succeeds()
    {
        var settings = new CosmosSourceSettings
        {
            ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=",
            Database = "db",
            Container = "container",
        };

        settings.Validate();
    }

    [TestMethod]
    public void Validate_WithAccountEndpoint_Succeeds()
    {
        var settings = new CosmosSourceSettings
        {
            UseRbacAuth = true,
            AccountEndpoint = "https://localhost:8081/",
            Database = "db",
            Container = "container",
        };

        settings.Validate();
    }

    [TestMethod]
    public void UseDefaultProxyCredentials_DefaultsToFalse()
    {
        var settings = new CosmosSourceSettings
        {
            ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=",
            Database = "db",
            Container = "container",
        };

        Assert.IsFalse(settings.UseDefaultProxyCredentials);
    }

    [TestMethod]
    public void UseDefaultProxyCredentials_CanBeSetToTrue()
    {
        var settings = new CosmosSourceSettings
        {
            ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=",
            Database = "db",
            Container = "container",
            UseDefaultProxyCredentials = true,
        };

        Assert.IsTrue(settings.UseDefaultProxyCredentials);
    }

    [TestMethod]
    public void UseDefaultCredentials_DefaultsToFalse()
    {
        var settings = new CosmosSourceSettings
        {
            ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=",
            Database = "db",
            Container = "container",
        };

        Assert.IsFalse(settings.UseDefaultCredentials);
    }

    [TestMethod]
    public void UseDefaultCredentials_CanBeSetToTrue()
    {
        var settings = new CosmosSourceSettings
        {
            ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=",
            Database = "db",
            Container = "container",
            UseDefaultCredentials = true,
        };

        Assert.IsTrue(settings.UseDefaultCredentials);
    }

    [TestMethod]
    public void PreAuthenticate_DefaultsToFalse()
    {
        var settings = new CosmosSourceSettings
        {
            ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=",
            Database = "db",
            Container = "container",
        };

        Assert.IsFalse(settings.PreAuthenticate);
    }

    [TestMethod]
    public void PreAuthenticate_CanBeSetToTrue()
    {
        var settings = new CosmosSourceSettings
        {
            ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=",
            Database = "db",
            Container = "container",
            PreAuthenticate = true,
        };

        Assert.IsTrue(settings.PreAuthenticate);
    }
}