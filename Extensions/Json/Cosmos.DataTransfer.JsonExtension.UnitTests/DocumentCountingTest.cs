using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Cosmos.DataTransfer.JsonExtension;
using Cosmos.DataTransfer.Common.UnitTests;
using Cosmos.DataTransfer.Common;

namespace Cosmos.DataTransfer.JsonExtension.UnitTests
{
    [TestClass]
    public class DocumentCountingTest
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
        public async Task JsonFormatWriter_CountsDocuments_LogsCorrectly()
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
                    { "DocumentProgressFrequency", "2" } // Log every 2 documents for testing
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
            var progressLogs = logs.Where(l => l.Contains("Processed") && l.Contains("documents for transfer")).ToList();
            var completionLogs = logs.Where(l => l.Contains("Completed processing") && l.Contains("total documents")).ToList();
            
            // Should have progress logs for documents 2 and 4 (every 2 documents)
            Assert.AreEqual(2, progressLogs.Count, "Should have 2 progress log entries");
            
            // Should have 1 completion log
            Assert.AreEqual(1, completionLogs.Count, "Should have 1 completion log entry");
            
            // Verify specific log content
            Assert.IsTrue(progressLogs[0].Contains("Processed 2 documents"));
            Assert.IsTrue(progressLogs[1].Contains("Processed 4 documents"));
            Assert.IsTrue(completionLogs[0].Contains("Completed processing 5 total documents"));
        }
    }
}