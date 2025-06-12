using Microsoft.VisualStudio.TestTools.UnitTesting;
using Cosmos.DataTransfer.AzureTableAPIExtension.Settings;

namespace Cosmos.DataTransfer.AzureTableAPIExtension.UnitTests
{
    [TestClass]
    public class AzureTableAPIDataSinkExtensionTests
    {
        [TestMethod]
        public void AzureTableAPIDataSinkSettings_ReplaceIfExists_DefaultsFalse()
        {
            var settings = new AzureTableAPIDataSinkSettings();
            
            Assert.IsFalse(settings.ReplaceIfExists ?? true, "ReplaceIfExists should default to false");
        }

        [TestMethod]
        public void AzureTableAPIDataSinkSettings_ReplaceIfExists_CanBeSetTrue()
        {
            var settings = new AzureTableAPIDataSinkSettings()
            {
                ReplaceIfExists = true
            };
            
            Assert.IsTrue(settings.ReplaceIfExists ?? false, "ReplaceIfExists should be settable to true");
        }

        [TestMethod]
        public void AzureTableAPIDataSinkSettings_ReplaceIfExists_CanBeSetFalse()
        {
            var settings = new AzureTableAPIDataSinkSettings()
            {
                ReplaceIfExists = false
            };
            
            Assert.IsFalse(settings.ReplaceIfExists ?? true, "ReplaceIfExists should be settable to false");
        }

        [TestMethod]
        public void AzureTableAPIDataSinkSettings_ReplaceIfExists_CanBeNull()
        {
            var settings = new AzureTableAPIDataSinkSettings()
            {
                ReplaceIfExists = null
            };
            
            Assert.IsNull(settings.ReplaceIfExists, "ReplaceIfExists should be settable to null");
        }
    }
}