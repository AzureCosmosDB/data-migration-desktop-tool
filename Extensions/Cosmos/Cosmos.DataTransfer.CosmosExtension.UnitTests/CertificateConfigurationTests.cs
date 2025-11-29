using System.ComponentModel.DataAnnotations;

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


        private class TestableCosmosSettings : CosmosSettingsBase
        {
        }
    }
}