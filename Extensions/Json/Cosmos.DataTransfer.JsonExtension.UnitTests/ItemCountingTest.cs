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
            var completionLogs = logs.Where(l => l.Contains("Completed formatting") && l.Contains("total items")).ToList();
            
            // Should have progress logs for items 2 and 4 (every 2 items)
            Assert.AreEqual(2, progressLogs.Count, "Should have 2 progress log entries");
            
            // Should have 1 completion log
            Assert.AreEqual(1, completionLogs.Count, "Should have 1 completion log entry");
            
            // Verify specific log content
            Assert.IsTrue(progressLogs[0].Contains("Formatted 2 items"));
            Assert.IsTrue(progressLogs[1].Contains("Formatted 4 items"));
            Assert.IsTrue(completionLogs[0].Contains("Completed formatting 5 total items"));
        }
    }
}