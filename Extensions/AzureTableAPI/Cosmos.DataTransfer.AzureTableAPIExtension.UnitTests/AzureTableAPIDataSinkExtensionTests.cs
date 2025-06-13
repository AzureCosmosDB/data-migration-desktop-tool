using Microsoft.VisualStudio.TestTools.UnitTesting;
using Cosmos.DataTransfer.AzureTableAPIExtension.Settings;

namespace Cosmos.DataTransfer.AzureTableAPIExtension.UnitTests
{
    [TestClass]
    public class AzureTableAPIDataSinkExtensionTests
    {
        [TestMethod]
        public void AzureTableAPIDataSinkSettings_WriteMode_DefaultsToCreate()
        {
            var settings = new AzureTableAPIDataSinkSettings();
            
            Assert.AreEqual(EntityWriteMode.Create, settings.WriteMode ?? EntityWriteMode.Replace, "WriteMode should default to Create");
        }

        [TestMethod]
        public void AzureTableAPIDataSinkSettings_WriteMode_CanBeSetToCreate()
        {
            var settings = new AzureTableAPIDataSinkSettings()
            {
                WriteMode = EntityWriteMode.Create
            };
            
            Assert.AreEqual(EntityWriteMode.Create, settings.WriteMode ?? EntityWriteMode.Replace, "WriteMode should be settable to Create");
        }

        [TestMethod]
        public void AzureTableAPIDataSinkSettings_WriteMode_CanBeSetToReplace()
        {
            var settings = new AzureTableAPIDataSinkSettings()
            {
                WriteMode = EntityWriteMode.Replace
            };
            
            Assert.AreEqual(EntityWriteMode.Replace, settings.WriteMode ?? EntityWriteMode.Create, "WriteMode should be settable to Replace");
        }

        [TestMethod]
        public void AzureTableAPIDataSinkSettings_WriteMode_CanBeSetToMerge()
        {
            var settings = new AzureTableAPIDataSinkSettings()
            {
                WriteMode = EntityWriteMode.Merge
            };
            
            Assert.AreEqual(EntityWriteMode.Merge, settings.WriteMode ?? EntityWriteMode.Create, "WriteMode should be settable to Merge");
        }

        [TestMethod]
        public void AzureTableAPIDataSinkSettings_WriteMode_CanBeNull()
        {
            var settings = new AzureTableAPIDataSinkSettings()
            {
                WriteMode = null
            };
            
            Assert.IsNull(settings.WriteMode, "WriteMode should be settable to null");
        }
    }
}