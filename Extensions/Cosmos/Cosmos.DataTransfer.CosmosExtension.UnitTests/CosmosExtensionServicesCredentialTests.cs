using Azure.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

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
    public void GetTokenCredentialSelection_WithWhitespaceServicePrincipalInfo_ReturnsDefaultCredential()
    {
        var settings = new CosmosSourceSettings
        {
            UseRbacAuth = true,
            AccountEndpoint = "https://localhost:8081/",
            Database = "db",
            Container = "container",
            TenantId = " ",
            ClientId = " ",
            ClientSecret = "client-secret",
        };

        var selection = CosmosExtensionServices.GetTokenCredentialSelection(settings);

        Assert.AreEqual(CosmosExtensionServices.TokenCredentialSelection.DefaultAzureCredential, selection);
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

    [TestMethod]
    public void CreateRbacTokenCredential_WithPasswordProtectedCertificate_ReturnsClientCertificateCredential()
    {
        var loggerMock = new Mock<ILogger>();
        const string certPassword = "test-password";
        var certPath = CreatePasswordProtectedPfx(certPassword);

        try
        {
            var settings = new CosmosSourceSettings
            {
                UseRbacAuth = true,
                AccountEndpoint = "https://localhost:8081/",
                Database = "db",
                Container = "container",
                TenantId = "tenant-id",
                ClientId = "client-id",
                ClientCertificatePath = certPath,
                ClientCertificatePassword = certPassword,
            };

            var credential = CosmosExtensionServices.CreateRbacTokenCredential(settings, loggerMock.Object);

            Assert.IsInstanceOfType<ClientCertificateCredential>(credential);
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(nameof(CosmosSourceSettings.ClientCertificatePassword))),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
        finally
        {
            if (File.Exists(certPath))
            {
                File.Delete(certPath);
            }
        }
    }

    [TestMethod]
    public void CreateRbacTokenCredential_WithCertificateWithoutPrivateKey_ThrowsFriendlyConfigurationError()
    {
        var loggerMock = new Mock<ILogger>();
        var certPath = CreatePublicCertificate();

        try
        {
            var settings = new CosmosSourceSettings
            {
                UseRbacAuth = true,
                AccountEndpoint = "https://localhost:8081/",
                Database = "db",
                Container = "container",
                TenantId = "tenant-id",
                ClientId = "client-id",
                ClientCertificatePath = certPath,
            };

            var ex = Assert.ThrowsException<InvalidOperationException>(() =>
                CosmosExtensionServices.CreateRbacTokenCredential(settings, loggerMock.Object));

            StringAssert.Contains(ex.Message, "Failed to configure RBAC credentials");
            Assert.IsInstanceOfType<CryptographicException>(ex.InnerException);
            StringAssert.Contains(ex.InnerException!.Message, "private key");
        }
        finally
        {
            if (File.Exists(certPath))
            {
                File.Delete(certPath);
            }
        }
    }

    [TestMethod]
    public void CreateClientOptions_UsesAllowBulkExecutionSetting()
    {
        var loggerMock = new Mock<ILogger>();
        var settings = new CosmosSourceSettings
        {
            ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=key",
            Database = "db",
            Container = "container",
            AllowBulkExecution = true,
        };

        var clientOptions = CosmosExtensionServices.CreateClientOptions(settings, "test-agent", loggerMock.Object);

        Assert.IsTrue(clientOptions.AllowBulkExecution);

        settings.AllowBulkExecution = false;
        clientOptions = CosmosExtensionServices.CreateClientOptions(settings, "test-agent", loggerMock.Object);

        Assert.IsFalse(clientOptions.AllowBulkExecution);
    }

    private static string CreatePasswordProtectedPfx(string password)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=unit-test-cert", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var pfxBytes = certificate.Export(X509ContentType.Pfx, password);
        var certPath = Path.Combine(Path.GetTempPath(), $"dmt-test-{Guid.NewGuid():N}.pfx");
        File.WriteAllBytes(certPath, pfxBytes);
        return certPath;
    }

    private static string CreatePublicCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=unit-test-cert", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var certBytes = certificate.Export(X509ContentType.Cert);
        var certPath = Path.Combine(Path.GetTempPath(), $"dmt-test-{Guid.NewGuid():N}.cer");
        File.WriteAllBytes(certPath, certBytes);
        return certPath;
    }
}
