using Azure.Data.Tables;
using Cosmos.DataTransfer.AzureTableAPIExtension.Data;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Parquet;

namespace Cosmos.DataTransfer.ParquetExtension.UnitTests
{
    [TestClass]
    public class RoundtripTests
    {
        private class TestLogger : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                Console.WriteLine(formatter(state, exception));
            }
        }

        [TestMethod]
        public async Task Roundtrip_ParquetToTableStorage_DateTimeShouldConvertToDateTimeOffset()
        {
            // Arrange - Write data with DateTimeOffset to Parquet
            var writer = new ParquetFormatWriter();
            var logger = new TestLogger();
            var config = new ConfigurationBuilder().Build();

            var testTimestamp = new DateTimeOffset(2024, 1, 15, 10, 30, 45, TimeSpan.FromHours(5));
            var sourceEntity = new TableEntity("partition1", "row1");
            sourceEntity["Name"] = "TestEntity";
            sourceEntity["Value"] = 100;
            sourceEntity.Timestamp = testTimestamp;

            var dataItem = new AzureTableAPIDataItem(sourceEntity, null, null);
            var dataItems = new[] { dataItem }.ToAsyncEnumerable();

            using var stream = new MemoryStream();

            // Act 1: Write to Parquet (converts DateTimeOffset to UTC DateTime)
            await writer.FormatDataAsync(dataItems, stream, config, logger, CancellationToken.None);

            // Act 2: Read from Parquet
            stream.Position = 0;
            using var parquetReader = await ParquetReader.CreateAsync(stream);
            using var rowGroupReader = parquetReader.OpenRowGroupReader(0);
            
            var readData = new Dictionary<string, object?>();
            foreach (var field in parquetReader.Schema.GetDataFields())
            {
                var column = await rowGroupReader.ReadColumnAsync(field);
                readData[field.Name] = column.Data.GetValue(0);
            }

            // Act 3: Convert back to TableEntity (simulating Parquet->TableStorage migration)
            var dataItemFromParquet = new ParquetDictionaryDataItem(readData);
            var targetEntity = dataItemFromParquet.ToTableEntity(null, null);

            // Assert
            Assert.IsTrue(readData.ContainsKey("Timestamp"), "Timestamp should be in Parquet data");
            
            var timestampFromParquet = readData["Timestamp"];
            Assert.IsNotNull(timestampFromParquet, "Timestamp should not be null");
            Assert.IsInstanceOfType(timestampFromParquet, typeof(DateTime), "Parquet stores as DateTime");
            
            var dateTimeValue = (DateTime)timestampFromParquet;
            Assert.AreEqual(DateTimeKind.Utc, dateTimeValue.Kind, "DateTime from Parquet should be UTC");
            
            // The key assertion: Can we add this DateTime to a TableEntity?
            // TableEntity accepts DateTime values just fine
            Assert.IsNotNull(targetEntity, "Should be able to create TableEntity");
            Assert.IsTrue(targetEntity.ContainsKey("Timestamp"), "TableEntity should have Timestamp");
            
            var storedValue = targetEntity["Timestamp"];
            Assert.IsInstanceOfType(storedValue, typeof(DateTime), "TableEntity stores DateTime as DateTime");
            
            // Verify the value is correct (should match the UTC time)
            var storedDateTime = (DateTime)storedValue;
            Assert.AreEqual(testTimestamp.UtcDateTime, storedDateTime, "UTC time should be preserved");
        }

        [TestMethod]
        public void DateTime_ImplicitConversion_ToDateTimeOffset()
        {
            // This test verifies that DateTime can be implicitly converted to DateTimeOffset
            // which is what would happen if Azure Table Storage needs to convert it
            
            var utcDateTime = new DateTime(2024, 1, 15, 10, 30, 45, DateTimeKind.Utc);
            
            // Should not throw - DateTime has implicit conversion to DateTimeOffset
            DateTimeOffset result = utcDateTime;
            
            Assert.AreEqual(utcDateTime, result.UtcDateTime);
            Assert.AreEqual(TimeSpan.Zero, result.Offset);
        }
    }
}
