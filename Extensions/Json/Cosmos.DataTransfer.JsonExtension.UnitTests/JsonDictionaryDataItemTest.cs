using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;

namespace Cosmos.DataTransfer.JsonExtension.UnitTests
{
    [TestClass]
    public class JsonDictionaryDataItemTest
    {
        [TestMethod]
        public void ConvertNumber()
        {
            var data = new Dictionary<string, object?>
            {
                { "integer", 1 },
                { "long", int.MaxValue * 2L },
                { "doubleAsNumber1", 1.0 },
                { "doubleAsNumber2", 1.1 },
            };

            var output = new JsonDictionaryDataItem(data);

            Assert.IsTrue(output.GetValue("integer")?.GetType() == typeof(int));
            Assert.IsTrue(output.GetValue("long")?.GetType() == typeof(long));
            Assert.IsTrue(output.GetValue("doubleAsNumber1")?.GetType() == typeof(double));
            Assert.IsTrue(output.GetValue("doubleAsNumber2")?.GetType() == typeof(double));
        }
    }
}