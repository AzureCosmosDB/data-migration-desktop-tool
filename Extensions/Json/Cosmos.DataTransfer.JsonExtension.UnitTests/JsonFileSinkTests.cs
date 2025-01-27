using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Common.UnitTests;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;

namespace Cosmos.DataTransfer.JsonExtension.UnitTests
{
    [TestClass]
    public class JsonFileSinkTests
    {
        [TestMethod]
        public async Task WriteAsync_WithFlatObjects_WritesToValidFile()
        {
            var sink = new JsonFileSink();

            var data = new List<DictionaryDataItem>
            {
                new(new Dictionary<string, object?>
                {
                    { "Id", 1 },
                    { "Name", "One" },
                }),
                new(new Dictionary<string, object?>
                {
                    { "Id", 2 },
                    { "Name", "Two" },
                }),
                new(new Dictionary<string, object?>
                {
                    { "Id", 3 },
                    { "Name", "Three" },
                }),
            };
            string outputFile = $"{DateTime.Now:yy-MM-dd}_FS_Output.json";
            var config = TestHelpers.CreateConfig(new Dictionary<string, string>
            {
                { "FilePath", outputFile }
            });

            await sink.WriteAsync(data.ToAsyncEnumerable(), config, new JsonFileSource(), NullLogger.Instance);

            var outputData = JsonConvert.DeserializeObject<List<TestDataObject>>(await File.ReadAllTextAsync(outputFile));

            Assert.IsTrue(outputData.Any(o => o.Id == 1 && o.Name == "One"));
            Assert.IsTrue(outputData.Any(o => o.Id == 2 && o.Name == "Two"));
            Assert.IsTrue(outputData.Any(o => o.Id == 3 && o.Name == "Three"));
        }
    }
}