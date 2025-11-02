using Cosmos.DataTransfer.CosmosExtension;
using System.ComponentModel.DataAnnotations;

namespace Cosmos.DataTransfer.CosmosExtension.UnitTests
{
    [TestClass]
    public class CertificateConfigurationTests
    {
        [TestMethod]
        public void CosmosSettingsBase_WithValidCertificatePath_ShouldValidateSuccessfully()
        {
            // Arrange - Create a temp certificate file for testing
            var tempCertPath = Path.GetTempFileName();
            File.WriteAllText(tempCertPath, "dummy cert content"); // Not a real cert, but file exists
            
            try
            {
                var settings = new TestableCosmosSettings
                {
                    ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDj...",
                    Database = "testDb",
                    Container = "testContainer",
                    CustomCertificatePath = tempCertPath,
                    DisableSslValidation = false
                };

                // Act
                var validationResults = settings.Validate(new ValidationContext(settings)).ToList();

                // Assert
                Assert.AreEqual(0, validationResults.Count, "Should have no validation errors");
            }
            finally
            {
                // Cleanup
                if (File.Exists(tempCertPath))
                    File.Delete(tempCertPath);
            }
        }

        [TestMethod]
        public void CosmosSettingsBase_WithInvalidCertificatePath_ShouldReturnValidationError()
        {
            // Arrange
            var settings = new TestableCosmosSettings
            {
                ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDj...",
                Database = "testDb",
                Container = "testContainer",
                CustomCertificatePath = "C:\\nonexistent\\path\\cert.cer",
                DisableSslValidation = false
            };

            // Act
            var validationResults = settings.Validate(new ValidationContext(settings)).ToList();

            // Assert
            Assert.AreEqual(1, validationResults.Count);
            Assert.IsTrue(validationResults[0].ErrorMessage!.Contains("CustomCertificatePath file does not exist"));
        }

        [TestMethod]
        public void CosmosSettingsBase_WithDisableSslValidation_ShouldValidateSuccessfully()
        {
            // Arrange
            var settings = new TestableCosmosSettings
            {
                ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDj...",
                Database = "testDb",
                Container = "testContainer",
                CustomCertificatePath = null,
                DisableSslValidation = true
            };

            // Act
            var validationResults = settings.Validate(new ValidationContext(settings)).ToList();

            // Assert
            Assert.AreEqual(0, validationResults.Count, "Should have no validation errors");
        }

        // Test implementation of CosmosSettingsBase for testing
        private class TestableCosmosSettings : CosmosSettingsBase
        {
        }
    }
}