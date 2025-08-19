using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Cosmos.DataTransfer.JsonExtension;
using Cosmos.DataTransfer.Common.UnitTests;
using Cosmos.DataTransfer.Common;

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

        [TestMethod]
        public async Task JsonFormatWriter_CountsItems_LogsCorrectly()
        {
            // Arrange
            var formatter = new JsonFormatWriter();
            var logger = new TestLogger();
            
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
            await formatter.FormatDataAsync(data.Cast<Cosmos.DataTransfer.Interfaces.IDataItem>().ToAsyncEnumerable(), stream, config, logger);
            
            // Assert
            stream.Position = 0;
            using var reader = new StreamReader(stream);
            var result = await reader.ReadToEndAsync();
            
            // Verify JSON was written correctly (basic sanity check)
            Assert.IsTrue(result.Contains("\"Id\":1"));
            Assert.IsTrue(result.Contains("\"Id\":5"));
            
            // Verify logging behavior
            var logs = logger.GetLogs();
            var progressLogs = logs.Where(l => l.Contains("Formatted") && l.Contains("items for transfer")).ToList();
            
            // Should have progress logs for items 2 and 4 (every 2 items)
            Assert.AreEqual(2, progressLogs.Count, "Should have 2 progress log entries");
            
            // Verify specific log content
            Assert.IsTrue(progressLogs[0].Contains("Formatted 2 items"));
            Assert.IsTrue(progressLogs[1].Contains("Formatted 4 items"));
            
            // Verify the item count is available for the sink to use
            Assert.AreEqual(5, ItemProgressTracker.ItemCount);
        }

        [TestMethod]
        public async Task FormatDataAsync_TracksItemsWithAzureBlobDetails_MakesCountAvailable()
        {
            // Arrange
            var logger = new TestLogger();
            var formatter = new JsonFormatWriter();
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
            await formatter.FormatDataAsync(data.Cast<Cosmos.DataTransfer.Interfaces.IDataItem>().ToAsyncEnumerable(), stream, config, logger);
            
            // Assert
            var logs = logger.GetLogs();
            var progressLogs = logs.Where(l => l.Contains("Formatted") && l.Contains("items for transfer")).ToList();
            
            // Should have 1 progress log for 2 items
            Assert.AreEqual(1, progressLogs.Count, "Should have 1 progress log entry");
            Assert.IsTrue(progressLogs[0].Contains("Formatted 2 items"));
            
            // Verify the item count is available for the sink to use
            Assert.AreEqual(3, ItemProgressTracker.ItemCount);
        }

        [TestMethod]
        public void ItemProgressTracker_SharesCountAcrossThreadBoundary()
        {
            // Arrange
            var logger = new TestLogger();
            
            // Act - Simulate format writer setting count
            ItemProgressTracker.Reset();
            ItemProgressTracker.Initialize(logger, 1000);
            ItemProgressTracker.IncrementItem();
            ItemProgressTracker.IncrementItem();
            ItemProgressTracker.IncrementItem();
            ItemProgressTracker.CompleteFormatting();
            
            // Assert - Simulate sink reading count
            var currentCount = ItemProgressTracker.ItemCount;
            Assert.AreEqual(3, currentCount, "Sink should be able to read the item count set by format writer");
        }
    }
}