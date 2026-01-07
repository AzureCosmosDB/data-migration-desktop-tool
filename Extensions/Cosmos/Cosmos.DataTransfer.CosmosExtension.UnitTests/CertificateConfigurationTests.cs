using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
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
            var loggerMock = new Mock<ILogger>();
            var settings = new TestableCosmosSettings
            {
                ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
                Database = "testDb",
                Container = "testContainer",
                DisableSslValidation = true
            };

            var client = CosmosExtensionServices.CreateClient(settings, "TestDisplay", loggerMock.Object);

            // Verify warning was logged when DisableSslValidation is true
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SSL certificate validation is DISABLED")),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            // Verify client was created successfully
            Assert.IsNotNull(client, "CosmosClient should be created");
        }

        [TestMethod]
        public void CreateClient_WithoutDisableSslValidation_DoesNotLogWarning()
        {
            var loggerMock = new Mock<ILogger>();
            var settings = new TestableCosmosSettings
            {
                ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
                Database = "testDb",
                Container = "testContainer",
                DisableSslValidation = false
            };

            var client = CosmosExtensionServices.CreateClient(settings, "TestDisplay", loggerMock.Object);

            // Verify warning was NOT logged when DisableSslValidation is false
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SSL certificate validation is DISABLED")),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);

            // Verify client was created successfully
            Assert.IsNotNull(client, "CosmosClient should be created");
        }

        private class TestableCosmosSettings : CosmosSettingsBase
        {
        }
    }
}