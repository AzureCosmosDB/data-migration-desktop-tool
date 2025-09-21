using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Cosmos.DataTransfer.JsonExtension;
using Cosmos.DataTransfer.Common.UnitTests;
using Cosmos.DataTransfer.Common;
using Cosmos.DataTransfer.Interfaces;

namespace Cosmos.DataTransfer.JsonExtension.UnitTests
{
    [TestClass]
    public class ItemCountingTest
    {
        private class TestLogger : ILogger
        {
            private readonly List<string> _logs = new List<string>();
            
            public IDisposable BeginScope<TState>(TState state) => null!;
            public bool IsEnabled(LogLevel logLevel) => true;
            
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                _logs.Add(formatter(state, exception));
            }
            
            public List<string> GetLogs() => _logs;
        }

        private class TestProgressReporter : IProgress<DataTransferProgress>
        {
            public DataTransferProgress? LastProgress { get; private set; }
            public List<DataTransferProgress> AllProgress { get; } = new List<DataTransferProgress>();

            public void Report(DataTransferProgress value)
            {
                LastProgress = value;
                AllProgress.Add(new DataTransferProgress(value.ItemCount, value.BytesTransferred, value.Message));
            }
        }

        [TestMethod]
        public async Task JsonFormatWriter_CountsItems_LogsCorrectly()
        {
            // Arrange
            var formatter = new JsonFormatWriter();
            var logger = new TestLogger();
            var progressReporter = new TestProgressReporter();
            
            var data = new List<DictionaryDataItem>
            {
                new(new Dictionary<string, object?> { { "Id", 1 }, { "Name", "One" } }),
                new(new Dictionary<string, object?> { { "Id", 2 }, { "Name", "Two" } }),
                new(new Dictionary<string, object?> { { "Id", 3 }, { "Name", "Three" } }),
                new(new Dictionary<string, object?> { { "Id", 4 }, { "Name", "Four" } }),
                new(new Dictionary<string, object?> { { "Id", 5 }, { "Name", "Five" } }),
            };

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "ItemProgressFrequency", "2" } // Log every 2 items for testing
                })
                .Build();

            using var stream = new MemoryStream();
            
            // Act
            await formatter.FormatDataAsync(data.Cast<Cosmos.DataTransfer.Interfaces.IDataItem>().ToAsyncEnumerable(), stream, config, logger, progressReporter);
            
            // Assert
            stream.Position = 0;
            using var reader = new StreamReader(stream);
            var result = await reader.ReadToEndAsync();
            
            // Verify JSON was written correctly (basic sanity check)
            Assert.IsTrue(result.Contains("\"Id\":1"));
            Assert.IsTrue(result.Contains("\"Id\":5"));
            
            // Verify progress reporting behavior
            var progressReports = progressReporter.AllProgress;
            
            // Should have progress reports for items 2, 4, and final count (5)
            Assert.AreEqual(3, progressReports.Count, "Should have 3 progress reports (2, 4, and final 5)");
            
            // Verify specific progress content
            Assert.AreEqual(2, progressReports[0].ItemCount);
            Assert.AreEqual(4, progressReports[1].ItemCount);
            Assert.AreEqual(5, progressReports[2].ItemCount);
            
            // Verify the final count is available 
            Assert.AreEqual(5, progressReporter.LastProgress?.ItemCount);
        }

        [TestMethod]
        public async Task FormatDataAsync_TracksItemsWithProgress_MakesCountAvailable()
        {
            // Arrange
            var logger = new TestLogger();
            var formatter = new JsonFormatWriter();
            var progressReporter = new TestProgressReporter();
            var data = new[]
            {
                new JsonDictionaryDataItem(new Dictionary<string, object?> { { "Id", 1 }, { "Name", "One" } }),
                new JsonDictionaryDataItem(new Dictionary<string, object?> { { "Id", 2 }, { "Name", "Two" } }),
                new JsonDictionaryDataItem(new Dictionary<string, object?> { { "Id", 3 }, { "Name", "Three" } }),
            };

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "ItemProgressFrequency", "2" }, // Log every 2 items for testing
                    { "BlobName", "test-data.json" },
                    { "ContainerName", "test-container" }
                })
                .Build();

            using var stream = new MemoryStream();
            
            // Act
            await formatter.FormatDataAsync(data.Cast<Cosmos.DataTransfer.Interfaces.IDataItem>().ToAsyncEnumerable(), stream, config, logger, progressReporter);
            
            // Assert
            var progressReports = progressReporter.AllProgress;
            
            // Should have progress reports for 2 items and final count (3)
            Assert.AreEqual(2, progressReports.Count, "Should have 2 progress reports (2 and final 3)");
            Assert.AreEqual(2, progressReports[0].ItemCount);
            Assert.AreEqual(3, progressReports[1].ItemCount);
            
            // Verify the final count is available for the sink to use
            Assert.AreEqual(3, progressReporter.LastProgress?.ItemCount);
        }

        [TestMethod]
        public void DataTransferProgressReporter_SharesCountAcrossContext()
        {
            // Arrange
            var logger = new TestLogger();
            var context = new DataTransferContext();
            var progressReporter = new DataTransferProgressReporter(logger, 1000, "item", context);
            
            // Act - Simulate format writer setting count
            progressReporter.Report(new DataTransferProgress(1));
            progressReporter.Report(new DataTransferProgress(2));
            progressReporter.Report(new DataTransferProgress(3));
            
            // Assert - Simulate sink reading count from context
            var currentProgress = context.GetCurrentProgress();
            Assert.AreEqual(3, currentProgress.ItemCount, "Sink should be able to read the item count set by format writer through context");
        }
    }
}