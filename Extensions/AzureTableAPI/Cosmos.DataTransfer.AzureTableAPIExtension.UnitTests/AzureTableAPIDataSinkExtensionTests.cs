using Microsoft.VisualStudio.TestTools.UnitTesting;
using Cosmos.DataTransfer.AzureTableAPIExtension.Settings;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

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

        [TestMethod]
        public void AzureTableAPIDataSinkSettings_WriteMode_JsonSerializationSupported()
        {
            // Test JSON to enum conversion for Create
            var jsonCreate = """{"WriteMode": "Create"}""";
            var configCreate = new ConfigurationBuilder()
                .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonCreate)))
                .Build();
            var settingsCreate = configCreate.Get<AzureTableAPIDataSinkSettings>();
            Assert.AreEqual(EntityWriteMode.Create, settingsCreate?.WriteMode, "WriteMode should be deserialized from JSON string 'Create'");

            // Test JSON to enum conversion for Replace
            var jsonReplace = """{"WriteMode": "Replace"}""";
            var configReplace = new ConfigurationBuilder()
                .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonReplace)))
                .Build();
            var settingsReplace = configReplace.Get<AzureTableAPIDataSinkSettings>();
            Assert.AreEqual(EntityWriteMode.Replace, settingsReplace?.WriteMode, "WriteMode should be deserialized from JSON string 'Replace'");

            // Test JSON to enum conversion for Merge
            var jsonMerge = """{"WriteMode": "Merge"}""";
            var configMerge = new ConfigurationBuilder()
                .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonMerge)))
                .Build();
            var settingsMerge = configMerge.Get<AzureTableAPIDataSinkSettings>();
            Assert.AreEqual(EntityWriteMode.Merge, settingsMerge?.WriteMode, "WriteMode should be deserialized from JSON string 'Merge'");
        }
    }
}