namespace Cosmos.DataTransfer.CosmosExtension.UnitTests
{
    [TestClass]
    public class CosmosDataSinkExtensionTests
    {
        [TestMethod]
        public void BuildObject_WithNestedArrays_WorksCorrectly()
        {
            var item = new CosmosDictionaryDataItem(new Dictionary<string, object?>()
            {
                {
                    "array",
                    new List<object?>
                    {
                        new List<object?>
                        {
                            new CosmosDictionaryDataItem(new Dictionary<string, object?>()
                            {
                                { "id", "sub1-1" }
                            }),
                            new CosmosDictionaryDataItem(new Dictionary<string, object?>()
                            {
                                { "id", "sub1-2" }
                            })
                        },
                        new List<object?>
                        {
                            new CosmosDictionaryDataItem(new Dictionary<string, object?>()
                            {
                                { "id", "sub2-1" }
                            }),
                        }
                    }
                }
            });

            dynamic obj = CosmosDataSinkExtension.BuildObject(item)!;

            Assert.AreEqual(typeof(object[]), obj.array.GetType());
            Assert.AreEqual(2, obj.array.Length);

            var firstSubArray = obj.array[0];
            Assert.AreEqual(typeof(object[]), firstSubArray.GetType());
            Assert.AreEqual(2, firstSubArray.Length);

            Assert.AreEqual("sub1-1", firstSubArray[0].id);
            Assert.AreEqual("sub1-2", firstSubArray[1].id);

            var secondSubArray = obj.array[1];
            Assert.AreEqual(typeof(object[]), secondSubArray.GetType());
            Assert.AreEqual(1, secondSubArray.Length);

            Assert.AreEqual("sub2-1", secondSubArray[0].id);
        }
    }
}
