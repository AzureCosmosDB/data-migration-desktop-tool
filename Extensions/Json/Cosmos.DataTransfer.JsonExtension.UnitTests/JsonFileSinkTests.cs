using Cosmos.DataTransfer.Common;
using Cosmos.DataTransfer.Common.UnitTests;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

        [TestMethod]
        public async Task WriteAsync_WithNestedDictionaries_SerializesCorrectly()
        {
            // Test case for the MongoDB nested elements issue
            var sink = new JsonFileSink();

            var data = new List<DictionaryDataItem>
            {
                new(new Dictionary<string, object?>
                {
                    { "_id", new Dictionary<string, object?> { { "$oid", "some_id" } } },
                    { "thread_id", "thread_id" },
                    { "content", new List<Dictionary<string, object?>>
                        {
                            new Dictionary<string, object?>
                            {
                                { "text", "a message text" },
                                { "type", "text" }
                            }
                        }
                    },
                    { "role", "user" }
                })
            };
            
            string outputFile = $"{DateTime.Now:yy-MM-dd}_FS_Nested_Output.json";
            var config = TestHelpers.CreateConfig(new Dictionary<string, string>
            {
                { "FilePath", outputFile }
            });

            await sink.WriteAsync(data.ToAsyncEnumerable(), config, new JsonFileSource(), NullLogger.Instance);

            var jsonContent = await File.ReadAllTextAsync(outputFile);
            var outputArray = JArray.Parse(jsonContent);
            
            Assert.AreEqual(1, outputArray.Count);
            
            var doc = outputArray[0] as JObject;
            Assert.IsNotNull(doc);
            
            // Verify _id is an object with $oid field
            var idObj = doc["_id"] as JObject;
            Assert.IsNotNull(idObj, "_id should be an object");
            Assert.AreEqual("some_id", idObj["$oid"]?.ToString());
            
            // Verify thread_id is a string
            Assert.AreEqual("thread_id", doc["thread_id"]?.ToString());
            
            // Verify content is an array of objects
            var contentArray = doc["content"] as JArray;
            Assert.IsNotNull(contentArray, "content should be an array");
            Assert.AreEqual(1, contentArray.Count);
            
            var contentItem = contentArray[0] as JObject;
            Assert.IsNotNull(contentItem, "content item should be an object");
            Assert.AreEqual("a message text", contentItem["text"]?.ToString());
            Assert.AreEqual("text", contentItem["type"]?.ToString());
            
            // Verify role is a string
            Assert.AreEqual("user", doc["role"]?.ToString());
        }
    }
}