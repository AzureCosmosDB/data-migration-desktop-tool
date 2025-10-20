using Azure.Data.Tables;
using Cosmos.DataTransfer.AzureTableAPIExtension.Data;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Parquet;

namespace Cosmos.DataTransfer.ParquetExtension.UnitTests
{
    [TestClass]
    public class ParquetFormatWriterTests
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
        public async Task FormatDataAsync_WithDateTimeOffsetField_ShouldNotThrow()
        {
            // Arrange
            var writer = new ParquetFormatWriter();
            var logger = new TestLogger();
            var config = new ConfigurationBuilder().Build();

            // Create a TableEntity with DateTimeOffset Timestamp
            var entity = new TableEntity("partition1", "row1");
            entity["CustomField"] = "CustomValue";
            entity["NumberField"] = 42;
            entity.Timestamp = DateTimeOffset.Now;

            var dataItem = new AzureTableAPIDataItem(entity, null, null);
            var dataItems = new[] { dataItem }.ToAsyncEnumerable();

            using var stream = new MemoryStream();

            // Act & Assert
            // This should not throw NotSupportedException for DateTimeOffset
            await writer.FormatDataAsync(dataItems, stream, config, logger, CancellationToken.None);

            // Verify that data was written
            Assert.IsTrue(stream.Length > 0, "Parquet data should have been written to stream");
        }

        [TestMethod]
        public async Task FormatDataAsync_WithMultipleTableEntities_ShouldHandleTimestamp()
        {
            // Arrange
            var writer = new ParquetFormatWriter();
            var logger = new TestLogger();
            var config = new ConfigurationBuilder().Build();

            // Create multiple TableEntity objects with different timestamps
            var entities = new List<IDataItem>();
            for (int i = 0; i < 5; i++)
            {
                var entity = new TableEntity($"partition{i}", $"row{i}");
                entity["Name"] = $"Entity{i}";
                entity["Value"] = i * 100;
                entity.Timestamp = DateTimeOffset.Now.AddMinutes(i);
                entities.Add(new AzureTableAPIDataItem(entity, null, null));
            }

            var dataItems = entities.ToAsyncEnumerable();
            using var stream = new MemoryStream();

            // Act & Assert
            await writer.FormatDataAsync(dataItems, stream, config, logger, CancellationToken.None);

            // Verify that data was written
            Assert.IsTrue(stream.Length > 0, "Parquet data should have been written to stream");
        }

        [TestMethod]
        public async Task FormatDataAsync_WithDateTimeOffset_ShouldConvertToDateTime()
        {
            // Arrange
            var writer = new ParquetFormatWriter();
            var logger = new TestLogger();
            var config = new ConfigurationBuilder().Build();

            // Use a non-zero offset to ensure UTC conversion is happening
            var testTimestamp = new DateTimeOffset(2024, 1, 15, 10, 30, 45, TimeSpan.FromHours(5));
            var entity = new TableEntity("partition1", "row1");
            entity["Name"] = "TestEntity";
            entity.Timestamp = testTimestamp;

            var dataItem = new AzureTableAPIDataItem(entity, null, null);
            var dataItems = new[] { dataItem }.ToAsyncEnumerable();

            using var stream = new MemoryStream();

            // Act
            await writer.FormatDataAsync(dataItems, stream, config, logger, CancellationToken.None);

            // Assert - Verify the Parquet file can be read
            stream.Position = 0;
            using var parquetReader = await ParquetReader.CreateAsync(stream);
            var schema = parquetReader.Schema;
            
            // Find the Timestamp field in the schema
            var timestampField = schema.Fields.FirstOrDefault(f => f.Name == "Timestamp");
            Assert.IsNotNull(timestampField, "Timestamp field should exist in schema");
            
            // Verify the type is DateTime, not DateTimeOffset
            var dataField = timestampField as Parquet.Schema.DataField;
            Assert.IsNotNull(dataField, "Timestamp field should be a DataField");
            Assert.AreEqual(typeof(DateTime), dataField.ClrType, "Timestamp should be stored as DateTime");
            
            // Read the actual data to verify the value
            using var rowGroupReader = parquetReader.OpenRowGroupReader(0);
            var timestampColumn = await rowGroupReader.ReadColumnAsync(dataField);
            
            // Verify the value is correct (converted from DateTimeOffset to UTC DateTime)
            var timestampValue = timestampColumn.Data.GetValue(0) as DateTime?;
            Assert.IsNotNull(timestampValue, "Timestamp value should not be null");
            Assert.AreEqual(testTimestamp.UtcDateTime, timestampValue.Value, "Timestamp value should match the original DateTimeOffset.UtcDateTime");
            
            // Verify it's different from the local DateTime (confirming UTC conversion happened)
            Assert.AreNotEqual(testTimestamp.DateTime, timestampValue.Value, "Timestamp should be converted to UTC, not preserve local time");
        }
    }
}
