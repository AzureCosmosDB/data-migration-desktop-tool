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
                    CertificatePath = tempCertPath,
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
                CertificatePath = "C:\\nonexistent\\path\\cert.cer",
                DisableSslValidation = false
            };

            // Act
            var validationResults = settings.Validate(new ValidationContext(settings)).ToList();

            // Assert
            Assert.AreEqual(1, validationResults.Count);
            Assert.IsTrue(validationResults[0].ErrorMessage!.Contains("CertificatePath file does not exist"));
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
                CertificatePath = null,
                DisableSslValidation = true
            };

            // Act
            var validationResults = settings.Validate(new ValidationContext(settings)).ToList();

            // Assert
            Assert.AreEqual(0, validationResults.Count, "Should have no validation errors");
        }

        [TestMethod]
        public void CosmosSettingsBase_WithValidPfxCertificatePath_ShouldValidateSuccessfully()
        {
            // Arrange - Create a temp PFX file for testing
            var tempPfxPath = Path.GetTempFileName();
            File.WriteAllText(tempPfxPath, "dummy pfx content"); // Not a real PFX, but file exists
            
            try
            {
                var settings = new TestableCosmosSettings
                {
                    ConnectionString = "AccountEndpoint=https://enterprise.cosmos.com:8081/;AccountKey=...",
                    Database = "testDb",
                    Container = "testContainer",
                    CertificatePath = tempPfxPath,
                    CertificatePassword = "testPassword"
                };

                // Act
                var validationResults = settings.Validate(new ValidationContext(settings)).ToList();

                // Assert
                Assert.AreEqual(0, validationResults.Count, "Should have no validation errors");
            }
            finally
            {
                // Cleanup
                if (File.Exists(tempPfxPath))
                    File.Delete(tempPfxPath);
            }
        }

        [TestMethod]
        public void CosmosSettingsBase_WithInvalidPfxCertificatePath_ShouldReturnValidationError()
        {
            // Arrange
            var settings = new TestableCosmosSettings
            {
                ConnectionString = "AccountEndpoint=https://enterprise.cosmos.com:8081/;AccountKey=...",
                Database = "testDb",
                Container = "testContainer",
                CertificatePath = "C:\\nonexistent\\path\\cert.pfx",
                CertificatePassword = "testPassword"
            };

            // Act
            var validationResults = settings.Validate(new ValidationContext(settings)).ToList();

            // Assert
            Assert.AreEqual(1, validationResults.Count);
            Assert.IsTrue(validationResults[0].ErrorMessage!.Contains("CertificatePath file does not exist"));
        }

        [TestMethod]
        public void CosmosSettingsBase_WithPfxCertificateWithoutPassword_ShouldValidateSuccessfully()
        {
            // Arrange - Create a temp PFX file for testing
            var tempPfxPath = Path.GetTempFileName();
            File.WriteAllText(tempPfxPath, "dummy pfx content"); // Not a real PFX, but file exists
            
            try
            {
                var settings = new TestableCosmosSettings
                {
                    ConnectionString = "AccountEndpoint=https://enterprise.cosmos.com:8081/;AccountKey=...",
                    Database = "testDb",
                    Container = "testContainer",
                    CertificatePath = tempPfxPath,
                    CertificatePassword = null // No password specified
                };

                // Act
                var validationResults = settings.Validate(new ValidationContext(settings)).ToList();

                // Assert
                Assert.AreEqual(0, validationResults.Count, "Should have no validation errors for PFX without password");
            }
            finally
            {
                // Cleanup
                if (File.Exists(tempPfxPath))
                    File.Delete(tempPfxPath);
            }
        }

        [TestMethod]
        public void CosmosSettingsBase_WithCertificateTypeDetection_ShouldSupportMultipleFormats()
        {
            // Arrange - Test that the unified CertificatePath supports different file extensions
            var extensions = new[] { ".cer", ".crt", ".pem", ".pfx", ".p12" };
            
            foreach (var extension in extensions)
            {
                var tempCertPath = Path.GetTempFileName();
                var certPathWithExtension = Path.ChangeExtension(tempCertPath, extension);
                File.Move(tempCertPath, certPathWithExtension);
                File.WriteAllText(certPathWithExtension, "dummy cert content");
                
                try
                {
                    var settings = new TestableCosmosSettings
                    {
                        ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDj...",
                        Database = "testDb",
                        Container = "testContainer",
                        CertificatePath = certPathWithExtension,
                        CertificatePassword = (extension == ".pfx" || extension == ".p12") ? "password" : null
                    };

                    // Act
                    var validationResults = settings.Validate(new ValidationContext(settings)).ToList();

                    // Assert
                    Assert.AreEqual(0, validationResults.Count, $"Should have no validation errors for {extension} files");
                }
                finally
                {
                    // Cleanup
                    if (File.Exists(certPathWithExtension))
                        File.Delete(certPathWithExtension);
                }
            }
        }

        // Test implementation of CosmosSettingsBase for testing
        private class TestableCosmosSettings : CosmosSettingsBase
        {
        }
    }
}