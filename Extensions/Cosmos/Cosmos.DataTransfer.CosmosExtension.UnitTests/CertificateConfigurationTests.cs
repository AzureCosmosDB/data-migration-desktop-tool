using Cosmos.DataTransfer.CosmosExtension;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Cosmos.DataTransfer.CosmosExtension.UnitTests
{
    [TestClass]
    public class CertificateConfigurationTests
    {
        [TestMethod]
        public void CosmosSettingsBase_WithDisableSslValidation_ShouldValidateSuccessfully()
        {
            var settings = new TestableCosmosSettings
            {
                ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDj...",
                Database = "testDb",
                Container = "testContainer",
                DisableSslValidation = true
            };

            var validationResults = settings.Validate(new ValidationContext(settings)).ToList();

            Assert.AreEqual(0, validationResults.Count, "Should have no validation errors");
        }

        [TestMethod]
        public void CertificateValidation_WithDisableSslValidation_ShouldBypassAllChecks()
        {
            var settings = new TestableCosmosSettings
            {
                ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDj...",
                Database = "testDb",
                Container = "testContainer",
                DisableSslValidation = true
            };

            var mockLogger = new Mock<ILogger>();
            var callback = CreateCertificateValidationCallbackForTesting(mockLogger.Object);

            using var cert = CreateDummyCertificate();
            var isValid = callback(cert, new X509Chain(), System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors);

            Assert.IsTrue(isValid, "Callback should always return true, bypassing all SSL errors");
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("SSL certificate validation is DISABLED")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public void CosmosSettingsBase_WithDisableSslValidationFalse_ShouldUseDefaultValidation()
        {
            var settings = new TestableCosmosSettings
            {
                ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDj...",
                Database = "testDb",
                Container = "testContainer",
                DisableSslValidation = false
            };

            var validationResults = settings.Validate(new ValidationContext(settings)).ToList();

            Assert.AreEqual(0, validationResults.Count, "Settings should validate with DisableSslValidation=false");
        }

        private static Func<X509Certificate2, X509Chain, System.Net.Security.SslPolicyErrors, bool> CreateCertificateValidationCallbackForTesting(
            ILogger logger)
        {
            var method = typeof(CosmosExtensionServices).GetMethod(
                "CreateCertificateValidationCallback",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var callback = method!.Invoke(null, new object[] { logger }) as Func<X509Certificate2, X509Chain, System.Net.Security.SslPolicyErrors, bool>;
            return callback!;
        }

        private static X509Certificate2 CreateDummyCertificate()
        {
            using var rsa = RSA.Create(2048);
            var certRequest = new CertificateRequest("CN=TestCert", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return certRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(365));
        }

        private class TestableCosmosSettings : CosmosSettingsBase
        {
        }
    }
}