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

        Assert.AreEqual(1, validationErrors.Count(v => v.Contains(nameof(CosmosSourceSettings.ConnectionString))));
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

        Assert.AreEqual(1, validationErrors.Count(v => v.Contains(nameof(CosmosSourceSettings.AccountEndpoint))));
    }

    [TestMethod]
    public void GetValidationErrors_WithRbacAuthAndIncompleteServicePrincipalInfo_ReturnsErrors()
    {
        var settings = new CosmosSourceSettings
        {
            UseRbacAuth = true,
            Database = "db",
            Container = "container",
            AccountEndpoint = "https://example.documents.azure.com:443/",
            TenantId = "tenant-id"
        };

        var validationErrors = settings.GetValidationErrors();
        
        Assert.AreEqual(1, validationErrors.Count(v => v.Contains(nameof(CosmosSourceSettings.TenantId)) && v.Contains(nameof(CosmosSourceSettings.ClientId))));
    }

    [TestMethod]
    public void GetValidationErrors_WithRbacAuthAndServicePrincipalButNoSecretOrCertificateInfo_ReturnsErrors()
    {
        var settings = new CosmosSourceSettings
        {
            UseRbacAuth = true,
            Database = "db",
            Container = "container",
            AccountEndpoint = "https://example.documents.azure.com:443/",
            TenantId = "tenant-id",
            ClientId = "client-id",
        };

        var validationErrors = settings.GetValidationErrors();
        
        Assert.AreEqual(1, validationErrors.Count(v => v.Contains(nameof(CosmosSourceSettings.ClientSecret)) && v.Contains(nameof(CosmosSourceSettings.ClientCertificatePath))));
    }

    [TestMethod]
    public void GetValidationErrors_WithRbacAuthAndServicePrincipalAndSecretAndCertificateInfo_ReturnsErrors()
    {
        var settings = new CosmosSourceSettings
        {
            UseRbacAuth = true,
            Database = "db",
            Container = "container",
            AccountEndpoint = "https://example.documents.azure.com:443/",
            TenantId = "tenant-id",
            ClientId = "client-id",
            ClientSecret = "client-secret",
            ClientCertificatePath = "./certs/cert.pfx",
        };

        var validationErrors = settings.GetValidationErrors();
        
        Assert.AreEqual(1, validationErrors.Count(v => v.Contains(nameof(CosmosSourceSettings.ClientSecret)) && v.Contains(nameof(CosmosSourceSettings.ClientCertificatePath))));
    }

    [TestMethod]
    public void GetValidationErrors_WithRbacAuthAndServicePrincipalAndPasswordButNoCertificate_ReturnsErrors()
    {
        var settings = new CosmosSourceSettings
        {
            UseRbacAuth = true,
            Database = "db",
            Container = "container",
            AccountEndpoint = "https://example.documents.azure.com:443/",
            TenantId = "tenant-id",
            ClientId = "client-id",
            ClientCertificatePassword = "client-secret-password",
        };

        var validationErrors = settings.GetValidationErrors();
        
        Assert.AreEqual(1, validationErrors.Count(v => v.Contains(nameof(CosmosSourceSettings.ClientCertificatePassword)) && v.Contains(nameof(CosmosSourceSettings.ClientCertificatePath))));
    }

    [TestMethod]
    public void GetValidationErrors_WithRbacAuthAndSecretOrCertificateButNoServicePrincipal_ReturnsErrors()
    {
        var settings = new CosmosSourceSettings
        {
            UseRbacAuth = true,
            Database = "db",
            Container = "container",
            AccountEndpoint = "https://example.documents.azure.com:443/",
            ClientSecret = "client-secret",
            ClientCertificatePath = "./certs/cert.pfx",
        };

        var validationErrors = settings.GetValidationErrors();
        
        Assert.AreEqual(1, validationErrors.Count(v => v.Contains(nameof(CosmosSourceSettings.TenantId)) && v.Contains(nameof(CosmosSourceSettings.ClientId))));
    }

    [TestMethod]
    public void GetValidationErrors_WithNoRbacAuthButHasServicePrincipal_ReturnsError()
    {
        var settings = new CosmosSourceSettings
        {
            ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=",
            Database = "db",
            Container = "container",
            AccountEndpoint = "https://example.documents.azure.com:443/",
            TenantId = "tenant-id"
        };

        var validationErrors = settings.GetValidationErrors();
        
        Assert.AreEqual(1, validationErrors.Count(v => v.Contains(nameof(CosmosSourceSettings.UseRbacAuth))));
    }

    [TestMethod]
    public void GetValidationErrors_WithRbacAuthAndServicePrincipalClientSecretInfo_ReturnsNoErrors()
    {
        var settings = new CosmosSourceSettings
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
        var settings = new CosmosSourceSettings
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