using Microsoft.VisualStudio.TestTools.UnitTesting;
using Cosmos.DataTransfer.AzureTableAPIExtension.Settings;
using Microsoft.Extensions.Configuration;

namespace Cosmos.DataTransfer.AzureTableAPIExtension.UnitTests
{
    [TestClass]
    public class AzureTableAPIDataSourceExtensionTests
    {
        [TestMethod]
        public void AzureTableAPIDataSourceSettings_QueryFilter_CanBeNull()
        {
            var settings = new AzureTableAPIDataSourceSettings();
            
            Assert.IsNull(settings.QueryFilter, "QueryFilter should be null by default");
        }

        [TestMethod]
        public void AzureTableAPIDataSourceSettings_QueryFilter_CanBeSet()
        {
            var settings = new AzureTableAPIDataSourceSettings()
            {
                QueryFilter = "PartitionKey eq 'test'"
            };
            
            Assert.AreEqual("PartitionKey eq 'test'", settings.QueryFilter, "QueryFilter should be settable");
        }

        [TestMethod]
        public void AzureTableAPIDataSourceSettings_QueryFilter_JsonDeserializationBasic()
        {
            // Test basic filter deserialization
            var json = """{"QueryFilter": "PartitionKey eq 'WI'"}""";
            var config = new ConfigurationBuilder()
                .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
                .Build();
            var settings = config.Get<AzureTableAPIDataSourceSettings>();
            
            Assert.AreEqual("PartitionKey eq 'WI'", settings?.QueryFilter, "QueryFilter should be deserialized from JSON");
        }

        [TestMethod]
        public void AzureTableAPIDataSourceSettings_QueryFilter_JsonDeserializationWithDatetime()
        {
            // Test datetime filter with JSON-escaped single quotes
            var json = """{"QueryFilter": "Timestamp eq datetime\u00272023-01-12T16:53:31.1714422Z\u0027"}""";
            var config = new ConfigurationBuilder()
                .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
                .Build();
            var settings = config.Get<AzureTableAPIDataSourceSettings>();
            
            Assert.AreEqual("Timestamp eq datetime'2023-01-12T16:53:31.1714422Z'", settings?.QueryFilter, 
                "QueryFilter with JSON-escaped datetime should be correctly deserialized");
        }

        [TestMethod]
        public void AzureTableAPIDataSourceSettings_QueryFilter_JsonDeserializationWithDatetimeGreaterThan()
        {
            // Test datetime filter with 'ge' (greater than or equal) operator
            var json = """{"QueryFilter": "Timestamp ge datetime\u00272023-05-15T03:30:32.663Z\u0027"}""";
            var config = new ConfigurationBuilder()
                .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
                .Build();
            var settings = config.Get<AzureTableAPIDataSourceSettings>();
            
            Assert.AreEqual("Timestamp ge datetime'2023-05-15T03:30:32.663Z'", settings?.QueryFilter, 
                "QueryFilter with 'ge' datetime operator should be correctly deserialized");
        }

        [TestMethod]
        public void AzureTableAPIDataSourceSettings_QueryFilter_JsonDeserializationWithDatetimeLessThan()
        {
            // Test datetime filter with 'lt' (less than) operator
            var json = """{"QueryFilter": "Timestamp lt datetime\u00272024-12-08T06:06:00.976Z\u0027"}""";
            var config = new ConfigurationBuilder()
                .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
                .Build();
            var settings = config.Get<AzureTableAPIDataSourceSettings>();
            
            Assert.AreEqual("Timestamp lt datetime'2024-12-08T06:06:00.976Z'", settings?.QueryFilter, 
                "QueryFilter with 'lt' datetime operator should be correctly deserialized");
        }

        [TestMethod]
        public void AzureTableAPIDataSourceSettings_QueryFilter_JsonDeserializationWithDatetimeRange()
        {
            // Test datetime filter with range (combining 'ge' and 'lt')
            var json = """{"QueryFilter": "Timestamp ge datetime\u00272023-01-01T00:00:00Z\u0027 and Timestamp lt datetime\u00272024-01-01T00:00:00Z\u0027"}""";
            var config = new ConfigurationBuilder()
                .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
                .Build();
            var settings = config.Get<AzureTableAPIDataSourceSettings>();
            
            Assert.AreEqual("Timestamp ge datetime'2023-01-01T00:00:00Z' and Timestamp lt datetime'2024-01-01T00:00:00Z'", settings?.QueryFilter, 
                "QueryFilter with datetime range should be correctly deserialized");
        }

        [TestMethod]
        public void AzureTableAPIDataSourceSettings_QueryFilter_JsonDeserializationCombinedFilters()
        {
            // Test combining partition key filter with datetime filter
            var json = """{"QueryFilter": "PartitionKey eq \u0027users\u0027 and Timestamp ge datetime\u00272023-05-15T00:00:00Z\u0027"}""";
            var config = new ConfigurationBuilder()
                .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
                .Build();
            var settings = config.Get<AzureTableAPIDataSourceSettings>();
            
            Assert.AreEqual("PartitionKey eq 'users' and Timestamp ge datetime'2023-05-15T00:00:00Z'", settings?.QueryFilter, 
                "QueryFilter combining partition key and datetime should be correctly deserialized");
        }
    }
}
