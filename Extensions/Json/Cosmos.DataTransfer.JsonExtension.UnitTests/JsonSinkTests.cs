using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Common.UnitTests;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;

namespace Cosmos.DataTransfer.JsonExtension.UnitTests
{
    [TestClass]
    public class JsonSinkTests
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
            string outputFile = $"{DateTime.Now:yy-MM-dd}_Output.json";
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

        [TestMethod]
        public async Task WriteAsync_WithSourceDates_PreservesDateFormats()
        {
            var sink = new JsonFileSink();

            var now = DateTime.UtcNow;
            var randomTime = DateTime.UtcNow.AddMinutes(Random.Shared.NextDouble() * 10000);
            var data = new List<DictionaryDataItem>
            {
                new(new Dictionary<string, object?>
                {
                    { "Id", 1 },
                    { "Created", now },
                }),
                new(new Dictionary<string, object?>
                {
                    { "Id", 2 },
                    { "Created", DateTime.UnixEpoch },
                }),
                new(new Dictionary<string, object?>
                {
                    { "Id", 3 },
                    { "Created", randomTime },
                }),
            };
            string outputFile = $"{now:yy-MM-dd}_DateOutput.json";
            var config = TestHelpers.CreateConfig(new Dictionary<string, string>
            {
                { "FilePath", outputFile }
            });

            await sink.WriteAsync(data.ToAsyncEnumerable(), config, new JsonFileSource(), NullLogger.Instance);

            string json = await File.ReadAllTextAsync(outputFile);
            var outputData = JsonConvert.DeserializeObject<List<TestDataObject>>(json);

            Assert.IsTrue(outputData.Any(o => o.Id == 1 && o.Created == now));
            Assert.IsTrue(outputData.Any(o => o.Id == 2 && o.Created == DateTime.UnixEpoch));
            Assert.IsTrue(outputData.Any(o => o.Id == 3 && o.Created == randomTime));
        }

        [TestMethod]
        public async Task WriteAsync_WithDateArray_PreservesDateFormats()
        {
            var sink = new JsonFileSink();

            var now = DateTime.UtcNow;
            var randomTime = DateTime.UtcNow.AddMinutes(Random.Shared.NextDouble() * 10000);
            var data = new List<DictionaryDataItem>
            {
                new(new Dictionary<string, object?>
                {
                    { "Id", 1 },
                    { "Dates", new[] { now, randomTime, DateTime.UnixEpoch } },
                })
            };

            string outputFile = $"{now:yy-MM-dd}_DateArrayOutput.json";
            var config = TestHelpers.CreateConfig(new Dictionary<string, string>
            {
                { "FilePath", outputFile }
            });

            await sink.WriteAsync(data.ToAsyncEnumerable(), config, new JsonFileSource(), NullLogger.Instance);

            string json = await File.ReadAllTextAsync(outputFile);
            var outputData = JsonConvert.DeserializeObject<List<TestDataObject>>(json);

            Assert.AreEqual(now, outputData?.Single().Dates?.ElementAt(0));
            Assert.AreEqual(randomTime, outputData?.Single().Dates?.ElementAt(1));
            Assert.AreEqual(DateTime.UnixEpoch, outputData?.Single().Dates?.ElementAt(2));
        }

        [TestMethod]
        public async Task WriteAsync_WithUnicode_PreservesOriginalCharacters()
        {
            var sink = new JsonFileSink();

            const string japanese = "炎ギ因";
            const string emoji = "✉✔✈";
            const string text = "XXX";
            var data = new List<DictionaryDataItem>
            {
                new(new Dictionary<string, object?>
                {
                    { "Id", 1 },
                    { "Array", new[] { japanese, emoji, text } },
                    { "Emoji", emoji },
                    { japanese, text },
                })
            };

            string outputFile = $"{DateTime.UtcNow:yy-MM-dd}_MixedUnicodeOutput.json";
            var config = TestHelpers.CreateConfig(new Dictionary<string, string>
            {
                { "FilePath", outputFile }
            });

            await sink.WriteAsync(data.ToAsyncEnumerable(), config, new JsonFileSource(), NullLogger.Instance);

            string json = await File.ReadAllTextAsync(outputFile);

            Assert.IsTrue(json.Contains(japanese));
            Assert.IsTrue(json.Contains(emoji));

            var outputData = JsonConvert.DeserializeObject<List<TestDataObject>>(json);

            var dataObject = outputData?.Single();
            Assert.AreEqual(japanese, dataObject?.Array?.ElementAt(0));
            Assert.AreEqual(emoji, dataObject?.Array?.ElementAt(1));
            Assert.AreEqual(emoji, dataObject?.Emoji);
            Assert.AreEqual(text, dataObject?.炎ギ因);
        }

        [TestMethod]
        public async Task WriteAsync_WithNullArrayValues_Succeeds()
        {
            var sink = new JsonFileSink();

            var now = DateTime.UtcNow;
            var randomTime = DateTime.UtcNow.AddMinutes(Random.Shared.NextDouble() * 10000);
            var data = new List<DictionaryDataItem>
            {
                new(new Dictionary<string, object?>
                {
                    { "Id", 1 },
                    { "Array", new[] { "A", null, "C", "D" } },
                })
            };

            string outputFile = $"{now:yy-MM-dd}_NullArrayOutput.json";
            var config = TestHelpers.CreateConfig(new Dictionary<string, string>
            {
                { "FilePath", outputFile }
            });

            await sink.WriteAsync(data.ToAsyncEnumerable(), config, new JsonFileSource(), NullLogger.Instance);

            string json = await File.ReadAllTextAsync(outputFile);
            var outputData = JsonConvert.DeserializeObject<List<TestDataObject>>(json);

            Assert.AreEqual("A", outputData?.Single().Array?.ElementAt(0));
            Assert.AreEqual(null, outputData?.Single().Array?.ElementAt(1));
            Assert.AreEqual("C", outputData?.Single().Array?.ElementAt(2));
        }
    }
}