using System.ComponentModel.DataAnnotations;

using Moq;

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

            [TestMethod]
            public void CreateClient_WithDisableSslValidation_LogsWarningAndSetsCallback()
            {
                var loggerMock = new Moq.Mock<Microsoft.Extensions.Logging.ILogger>();
                var settings = new TestableCosmosSettings
                {
                    ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDj...",
                    Database = "testDb",
                    Container = "testContainer",
                    DisableSslValidation = true
                };

                var client = Cosmos.DataTransfer.CosmosExtension.CosmosExtensionServices.CreateClient(settings, "TestDisplay", loggerMock.Object);

                // Verify warning was logged
                loggerMock.Verify(
                    l => l.Log(
                        Microsoft.Extensions.Logging.LogLevel.Warning,
                        Moq.It.IsAny<Microsoft.Extensions.Logging.EventId>(),
                        Moq.It.Is<Moq.It.IsAnyType>((v, t) => v.ToString().Contains("SSL certificate validation is DISABLED")),
                        Moq.It.IsAny<System.Exception>(),
                        Moq.It.IsAny<System.Func<Moq.It.IsAnyType, System.Exception, string>>()),
                    Moq.Times.AtLeastOnce);

                // Use reflection to get the ServerCertificateCustomValidationCallback
                var optionsField = typeof(Microsoft.Azure.Cosmos.CosmosClient).GetField("clientOptions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var clientOptions = optionsField?.GetValue(client) as Microsoft.Azure.Cosmos.CosmosClientOptions;
                Assert.IsNotNull(clientOptions, "CosmosClientOptions should not be null");
                Assert.IsNotNull(clientOptions.ServerCertificateCustomValidationCallback, "ServerCertificateCustomValidationCallback should be set");
            }

        private class TestableCosmosSettings : CosmosSettingsBase
        {
        }
    }
}