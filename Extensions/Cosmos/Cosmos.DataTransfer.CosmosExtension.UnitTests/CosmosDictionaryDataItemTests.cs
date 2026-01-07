using Cosmos.DataTransfer.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cosmos.DataTransfer.CosmosExtension.UnitTests
{
    [TestClass]
    public class CosmosDictionaryDataItemTests
    {
        [TestMethod]
        public async Task GetFieldNames_WithFlatObject_ReportsCorrectNames()
        {
            const string fileIn = "Data/IdName.json";

            var json = JObject.Parse(await File.ReadAllTextAsync(fileIn));

            var item = new CosmosDictionaryDataItem(json.ToObject<Dictionary<string, object?>>());

            var fields = item.GetFieldNames().ToList();

            Assert.AreEqual(2, fields.Count);
            CollectionAssert.Contains(fields, "id");
            CollectionAssert.Contains(fields, "name");
        }

        [TestMethod]
        public async Task GetValue_WithFlatObject_ReturnsValidValues()
        {
            const string fileIn = "Data/IdName.json";

            var json = JObject.Parse(await File.ReadAllTextAsync(fileIn));

            var item = new CosmosDictionaryDataItem(json.ToObject<Dictionary<string, object?>>());

            Assert.AreEqual(1L, item.GetValue("id"));
            Assert.AreEqual("One", item.GetValue("name"));
        }

        [TestMethod]
        public async Task GetFieldNames_WithNestedObject_ReportsParentAndChildNames()
        {
            const string fileIn = "Data/Nested.json";

            var json = JObject.Parse(await File.ReadAllTextAsync(fileIn));

            var item = new CosmosDictionaryDataItem(json.ToObject<Dictionary<string, object?>>());

            var fields = item.GetFieldNames().ToList();

            Assert.AreEqual(3, fields.Count);
            CollectionAssert.Contains(fields, "id");
            CollectionAssert.Contains(fields, "name");
            CollectionAssert.Contains(fields, "child");

            var child = item.GetValue("child") as IDataItem;
            Assert.IsNotNull(child);
            var childFields = child.GetFieldNames().ToList();
            Assert.AreEqual(2, childFields.Count);
            CollectionAssert.Contains(childFields, "type");
            CollectionAssert.Contains(childFields, "data");
        }

        [TestMethod]
        public async Task GetValue_WithMixedValueTypes_ReturnsValidTypedValues()
        {
            const string fileIn = "Data/MixedTypes.json";

            var json = JObject.Parse(await File.ReadAllTextAsync(fileIn));

            var item = new CosmosDictionaryDataItem(json.ToObject<Dictionary<string, object?>>());

            Assert.AreEqual(2L, item.GetValue("id"));
            Assert.AreEqual("Matt", item.GetValue("name"));

            object? arrayValue = item.GetValue("otherNames");
            var array = arrayValue as IEnumerable<object>;
            Assert.IsNotNull(array);
            Assert.AreEqual(3, array.Count());
            CollectionAssert.DoesNotContain(array.Select(a => a is string).ToList(), false);
        
            object? mixedArrayValue = item.GetValue("mixed");
            var mixedArray = mixedArrayValue as IEnumerable<object>;
            Assert.IsNotNull(mixedArray);
            Assert.AreEqual(5, mixedArray.Count());
            Assert.AreEqual(1L, mixedArray.ElementAt(0));
            Assert.AreEqual(true, mixedArray.ElementAt(1));
            Assert.AreEqual(3L, mixedArray.ElementAt(2));
            Assert.AreEqual("four", mixedArray.ElementAt(3));
            Assert.IsInstanceOfType(mixedArray.ElementAt(4), typeof(IDataItem));
        }

        [TestMethod]
        public async Task GetFieldNames_WithTypeHintProperty_IncludesTypeField()
        {
            const string fileIn = "Data/TypeHintProperty.json";

            var json = JObject.Parse(await File.ReadAllTextAsync(fileIn));

            // Use shared utility to convert JObject to Dictionary while preserving $type properties
            var dict = CosmosDictionaryDataItem.JObjectToDictionary(json);
            var item = new CosmosDictionaryDataItem(dict);

            var fields = item.GetFieldNames().ToList();

            Assert.AreEqual(3, fields.Count);
            CollectionAssert.Contains(fields, "id");
            CollectionAssert.Contains(fields, "name");
            CollectionAssert.Contains(fields, "myFavouritePet");

            var child = item.GetValue("myFavouritePet") as IDataItem;
            Assert.IsNotNull(child);
            var childFields = child.GetFieldNames().ToList();
            Assert.AreEqual(3, childFields.Count);
            CollectionAssert.Contains(childFields, "$type");
            CollectionAssert.Contains(childFields, "Name");
            CollectionAssert.Contains(childFields, "OtherName");
            
            Assert.AreEqual("MyProject.Pets.Dog, MyProject", child.GetValue("$type"));
            Assert.AreEqual("Foo", child.GetValue("Name"));
            Assert.AreEqual("OtherFoo", child.GetValue("OtherName"));
        }
    }
}