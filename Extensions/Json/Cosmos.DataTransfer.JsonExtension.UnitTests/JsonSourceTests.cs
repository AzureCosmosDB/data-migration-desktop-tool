using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Common.UnitTests;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cosmos.DataTransfer.JsonExtension.UnitTests
{
    [TestClass]
    public class JsonSourceTests
    {
        [TestMethod]
        public async Task ReadAsync_WithFlatObjects_ReadsValues()
        {
            var extension = new JsonFileSource();
            var config = TestHelpers.CreateConfig(new Dictionary<string, string>
            {
                { "FilePath", "Data/SimpleIdName.json" }
            });

            await foreach (var dataItem in extension.ReadAsync(config, NullLogger.Instance))
            {
                CollectionAssert.AreEquivalent(new[] { "id", "name" }, dataItem.GetFieldNames().ToArray());
                Assert.IsNotNull(dataItem.GetValue("id"));
                Assert.IsNotNull(dataItem.GetValue("name"));
            }
        }

        [TestMethod]
        public async Task ReadAsync_WithNestedObjects_ReadsValues()
        {
            var extension = new JsonFileSource();
            var config = TestHelpers.CreateConfig(new Dictionary<string, string>
            {
                { "FilePath", "Data/NestedObjects.json" }
            });

            await foreach (var dataItem in extension.ReadAsync(config, NullLogger.Instance))
            {
                if (dataItem.GetValue("child") is IDataItem child)
                {
                    CollectionAssert.AreEquivalent(new[] { "type", "data" }, child.GetFieldNames().ToArray());
                    Assert.IsNotNull(child.GetValue("type"));
                    Assert.IsNotNull(child.GetValue("data"));
                }
                else
                {
                    Assert.Fail();
                }
            }
        }

        [TestMethod]
        public async Task ReadAsync_WithSingleObjectFile_ReadsValues()
        {
            var extension = new JsonFileSource();
            var config = TestHelpers.CreateConfig(new Dictionary<string, string>
            {
                { "FilePath", "Data/SingleObject.json" }
            });

            int counter = 0;
            await foreach (var dataItem in extension.ReadAsync(config, NullLogger.Instance))
            {
                counter++;
                CollectionAssert.AreEquivalent(new[] { "id", "name" }, dataItem.GetFieldNames().ToArray());
                Assert.IsInstanceOfType(dataItem.GetValue("id"), typeof(int));
                Assert.IsNotNull(dataItem.GetValue("name"));
            }

            Assert.AreEqual(1, counter);

        }

        [TestMethod]
        public async Task ReadAsync_WithSingleObjectsFolder_ReadsValuesInOrder()
        {
            var extension = new JsonFileSource();
            var config = TestHelpers.CreateConfig(new Dictionary<string, string>
            {
                { "FilePath", "Data/SingleObjects" }
            });

            int counter = 0;
            double lastId = -1;
            await foreach (var dataItem in extension.ReadAsync(config, NullLogger.Instance))
            {
                counter++;
                CollectionAssert.AreEquivalent(new[] { "id", "name" }, dataItem.GetFieldNames().ToArray());
                object? value = dataItem.GetValue("id");
                Assert.IsInstanceOfType(value, typeof(int));
                Assert.IsNotNull(dataItem.GetValue("name"));
                var current = (int?)value ?? int.MaxValue;
                Assert.IsTrue(current > lastId);
                lastId = current;
            }

            Assert.AreEqual(4, counter);
        }

        [TestMethod]
        public async Task ReadAsync_WithArraysFolder_ReadsValues()
        {
            var extension = new JsonFileSource();
            var config = TestHelpers.CreateConfig(new Dictionary<string, string>
            {
                { "FilePath", "Data/Arrays" }
            });

            int counter = 0;
            await foreach (var dataItem in extension.ReadAsync(config, NullLogger.Instance))
            {
                counter++;
                CollectionAssert.AreEquivalent(new[] { "id", "name" }, dataItem.GetFieldNames().ToArray());
                Assert.IsInstanceOfType(dataItem.GetValue("id"), typeof(int));
                Assert.IsNotNull(dataItem.GetValue("name"));
            }

            Assert.AreEqual(5, counter);
        }

        [TestMethod]
        public async Task ReadAsync_WithMixedObjectsFolder_ReadsValues()
        {
            var extension = new JsonFileSource();
            var config = TestHelpers.CreateConfig(new Dictionary<string, string>
            {
                { "FilePath", "Data/MixedObjects" }
            });

            int counter = 0;
            await foreach (var dataItem in extension.ReadAsync(config, NullLogger.Instance))
            {
                counter++;
                CollectionAssert.AreEquivalent(new[] { "id", "name" }, dataItem.GetFieldNames().ToArray());
                Assert.IsInstanceOfType(dataItem.GetValue("id"), typeof(int));
                Assert.IsNotNull(dataItem.GetValue("name"));
            }

            Assert.AreEqual(5, counter);
        }

        [TestMethod]
        public async Task ReadAsync_WithFlatObjects_ReadsValuesFromUrl()
        {
            var extension = new JsonFileSource();
            var config = TestHelpers.CreateConfig(new Dictionary<string, string>
            {
                { "FilePath", "https://raw.githubusercontent.com/Azure/azure-documentdb-datamigrationtool/main/Extensions/Json/.JsonExtension.UnitTests/Data/SimpleIdName.json" }
            });

            await foreach (var dataItem in extension.ReadAsync(config, NullLogger.Instance))
            {
                CollectionAssert.AreEquivalent(new[] { "id", "name" }, dataItem.GetFieldNames().ToArray());
                Assert.IsNotNull(dataItem.GetValue("id"));
                Assert.IsNotNull(dataItem.GetValue("name"));
            }
        }

        [TestMethod]
        public async Task ReadAsync_WithTypeHintFields_IncludesAllInOutput()
        {
            var json = @"[
{
    ""id"": 1,
    ""name"": ""One"",
    ""$type"": ""Number"",
    ""data"": {
        ""$type"": ""Object"",
        ""name"": ""A""
    }
},
{
    ""id"": 2,
    ""name"": ""Two"",
    ""$type"": ""Digit"",
    ""data"": {
        ""$type"": ""String"",
        ""name"": ""B""
    }
}
]";
            var filePath = Path.Combine(Path.GetTempPath(), "TypeHintFields.json");

            await File.WriteAllTextAsync(filePath, json);

            var extension = new JsonFileSource();
            var config = TestHelpers.CreateConfig(new Dictionary<string, string>
            {
                { "FilePath", filePath }
            });

            int counter = 0;
            await foreach (var dataItem in extension.ReadAsync(config, NullLogger.Instance))
            {
                counter++;
                var fields = dataItem.GetFieldNames().ToArray();
                CollectionAssert.AreEquivalent(new[] { "id", "name", "$type", "data" }, fields);
                Assert.IsNotNull(dataItem.GetValue("id"));
                Assert.IsNotNull(dataItem.GetValue("name"));
                Assert.IsNotNull(dataItem.GetValue("$type"));
                var child = dataItem.GetValue("data") as JsonDictionaryDataItem;
                Assert.IsNotNull(child);
                CollectionAssert.AreEquivalent(new[] { "$type", "name" }, child.GetFieldNames().ToArray());
                Assert.IsNotNull(child.GetValue("$type"));
                Assert.IsNotNull(child.GetValue("name"));
            }

            Assert.AreEqual(2, counter);
        }
    }
}