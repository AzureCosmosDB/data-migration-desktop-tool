using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cosmos.DataTransfer.Common.UnitTests;

[TestClass]
public class DataItemJsonConverterTests
{
    [TestMethod]
    [DataRow(13, 13, true)]
    [DataRow(1L, 1, false)]
    [DataRow(2.178f, 1, false)]
    [DataRow(2.178, 1, false)]
    [DataRow("string", 1, false)]
    public void Test_TryGetInteger(object x, int expected, bool success) {
        int ret;
        var result = DataItemJsonConverter.TryGetInteger(x, out ret);
        Assert.AreEqual(success, result);
        if (success) {
            Assert.AreEqual(expected, ret);
        }
    }

    private static (Utf8JsonWriter,Func<string>) CreateUtf8JsonWriter() {
        var stream = new MemoryStream();
        var writer = new Utf8JsonWriter(stream, new JsonWriterOptions() {
            Indented = false,
            SkipValidation = true
        });
        
        var read = () => {
            writer.Flush();
            stream.Seek(0, SeekOrigin.Begin);
            using StreamReader reader = new(stream);
            var s = reader.ReadToEnd();
            stream.Close();
            return s;
        };
        return (writer, read);
    }

    public static IEnumerable<object?[]> Test_WriteFieldValue_Data { get
    {
        yield return new object[] { 1, "\"x\":1" };
        yield return new object[] { -99, "\"x\":-99"  };
        yield return new object?[] { null, ""  };
        yield return new object?[] { null, "\"x\":null", true };
        yield return new object[] { 173927362400, "\"x\":173927362400"  };
        yield return new object[] { -173927362400, "\"x\":-173927362400"  };
        yield return new object[] { 3ul, "\"x\":3"  };
        yield return new object[] { 3u, "\"x\":3"  };
        yield return new object[] { 2.718f, "\"x\":2.7179999351501465"  };
        yield return new object[] { 2.718, "\"x\":2.718"  };
        yield return new object[] { 6.022e23, "\"x\":6.022E+23"  };
        yield return new object[] { 1.66053906892e-27, "\"x\":1.66053906892E-27"  };
        yield return new object[] { 2.718m, "\"x\":2.718" };
        yield return new object[] { -2.718m, "\"x\":-2.718" };
        yield return new object[] { (short)2, "\"x\":2" };
        yield return new object[] { (short)-2, "\"x\":-2" };
        yield return new object[] { (byte)42, "\"x\":42" };
        yield return new object[] { (sbyte)-42, "\"x\":-42" };
        yield return new object[] { true, "\"x\":true" };
        yield return new object[] { false, "\"x\":false" };
        yield return new object[] { new DateTime(2025, 10, 15, 16, 45, 12, 666, DateTimeKind.Unspecified), "\"x\":\"2025-10-15T16:45:12.6660000\"" };
        yield return new object[] { new DateTime(2025, 10, 15, 16, 45, 12, 666, DateTimeKind.Utc), "\"x\":\"2025-10-15T16:45:12.6660000Z\"" };
        yield return new object[] { new DateTimeOffset(2025, 10, 15, 16, 45, 12, 666, TimeSpan.Zero), "\"x\":\"2025-10-15T16:45:12.6660000+00:00\"" };
        yield return new object[] { new DateTimeOffset(2025, 10, 15, 16, 45, 12, 666, TimeSpan.FromHours(5)), "\"x\":\"2025-10-15T16:45:12.6660000+05:00\"" };
        yield return new object[] { 'a', "\"x\":\"a\"" };
        yield return new object[] { "Greetings human    ", "\"x\":\"Greetings human    \"" };
        // yield return new object[] { true, "\"x\":true" };
    } }

    [TestMethod]
    [DynamicData(nameof(Test_WriteFieldValue_Data))]
    public void Test_WriteFieldValue(object? obj, string expected, bool includeNullFields = false) {
        var (writer, readFunc) = CreateUtf8JsonWriter();

        DataItemJsonConverter.WriteFieldValue(writer, "x", obj, includeNullFields: includeNullFields);
        Assert.AreEqual(expected, readFunc(), $"Input type: {obj?.GetType()}."); 
    }

    [TestMethod]
    public void Test_WriteFieldValue2() {
        var (writer, readFunc) = CreateUtf8JsonWriter();
        var obj = (ushort)2;
        var expected = "\"x\":2";

        DataItemJsonConverter.WriteFieldValue(writer, "x", obj, includeNullFields: false);
        Assert.AreEqual(expected, readFunc(), $"Input type: {obj.GetType()}."); 
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    /// null values in arrays are kept regardless of includenullFields.
    public void Test_WriteFieldValue_ArrayOfBitsAndPieces(bool includeNullFields) {
        var objects = Test_WriteFieldValue_Data
            .Where(x => (string)x[1]! != "")
            .Select(x => x[0]).ToArray();
        Assert.IsNotNull(objects);
        var pattern = "\"x\":(.*)$";
        var valExtract = Test_WriteFieldValue_Data
            .Select(x => Regex.Match((string)x[1]!, pattern).Groups[1].Value)
            .Where(x => x.Length > 0)
            .ToArray();
        var expected = "\"x\":[" + String.Join(',', valExtract) + "]";
        
        
        var (writer, readFunc) = CreateUtf8JsonWriter();
        DataItemJsonConverter.WriteFieldValue(writer, "x", objects, includeNullFields: includeNullFields);
        Assert.AreEqual(expected, readFunc(), $"includeNullFields: {includeNullFields}");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]    
    public void Test_WriteFieldValue_DataItem(bool includeNullFields) {
        var obj = new DictionaryDataItem(new Dictionary<string, object?> {
            { "long", 173927362400 },
            { "NULL", null },
            { "foo", "bar" },
            { "small_pi", 3.1 }
        });
        var expected = "\"x\":{\"long\":173927362400,\"NULL\":null,\"foo\":\"bar\",\"small_pi\":3.1}";
        if (!includeNullFields) {
            expected = expected.Replace("\"NULL\":null,", "");
        }
        var (writer, readFunc) = CreateUtf8JsonWriter();
        DataItemJsonConverter.WriteFieldValue(writer, "x", obj, includeNullFields: includeNullFields);
        Assert.AreEqual(expected, readFunc(), $"includeNullFields: {includeNullFields}");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Test_WriteFieldValue_NestedDataItem(bool includeNullFields) {
        var obj = new DictionaryDataItem(new Dictionary<string, object?> {
            { "long", 173927362400 },
            { "NULL", null },
            { "foo", "bar" },
            { "small_pi", 3.1 }
        });
        obj.Items.Add("obj", new DictionaryDataItem(obj.Items.ToDictionary(x => x.Key, x => x.Value)));
        var expected = "\"x\":{\"long\":173927362400,\"NULL\":null,\"foo\":\"bar\",\"small_pi\":3.1," +
            "\"obj\":{\"long\":173927362400,\"NULL\":null,\"foo\":\"bar\",\"small_pi\":3.1}}";
        if (!includeNullFields) {
            expected = expected.Replace("\"NULL\":null,", "");
        }
        var (writer, readFunc) = CreateUtf8JsonWriter();
        DataItemJsonConverter.WriteFieldValue(writer, "x", obj, includeNullFields: includeNullFields);
        Assert.AreEqual(expected, readFunc(), $"includeNullFields: {includeNullFields}");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Test_WriteFieldValue_ArrayedDataItem(bool includeNullFields) {
        var obj = new DictionaryDataItem(new Dictionary<string, object?> {
            { "NULL", null },
            { "small_pi", 3.1 },
            { "arr", new object?[] { 
                null, 
                new DictionaryDataItem(new Dictionary<string, object?> {
                    { "NULL", null },
                    { "foo", "bar" }
                }) 
            }}
        });

        var expected = "\"x\":{\"NULL\":null,\"small_pi\":3.1," +
            "\"arr\":[null,{\"NULL\":null,\"foo\":\"bar\"}]}";
        if (!includeNullFields) {
            expected = expected.Replace("\"NULL\":null,", "");
        }
        var (writer, readFunc) = CreateUtf8JsonWriter();
        DataItemJsonConverter.WriteFieldValue(writer, "x", obj, includeNullFields: includeNullFields);
        Assert.AreEqual(expected, readFunc(), $"includeNullFields: {includeNullFields}");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Test_AsJsonString(bool includeNullFields) {
        var obj = new DictionaryDataItem(new Dictionary<string, object?> {
            { "NULL", null },
            { "small_pi", 3.1 },
        });

        var expected = "{\"NULL\":null,\"small_pi\":3.1}";
        if (!includeNullFields) {
            expected = expected.Replace("\"NULL\":null,", "");
        }
        var json = DataItemJsonConverter.AsJsonString(obj, false, includeNullFields);
        Assert.AreEqual(expected, json);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Test_WriteFieldValue_DictionaryAsNestedObject(bool includeNullFields)
    {
        // Test that Dictionary<string, object?> is properly serialized as nested object
        var nestedDict = new Dictionary<string, object?>
        {
            { "text", "a message text" },
            { "type", "text" },
            { "NULL", null }
        };
        
        var expected = "\"x\":{\"text\":\"a message text\",\"type\":\"text\",\"NULL\":null}";
        if (!includeNullFields)
        {
            expected = expected.Replace(",\"NULL\":null", "");
        }
        
        var (writer, readFunc) = CreateUtf8JsonWriter();
        DataItemJsonConverter.WriteFieldValue(writer, "x", nestedDict, includeNullFields: includeNullFields);
        Assert.AreEqual(expected, readFunc(), $"includeNullFields: {includeNullFields}");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Test_WriteFieldValue_ArrayOfDictionaries(bool includeNullFields)
    {
        // Test array of dictionaries (simulating MongoDB nested array scenario)
        var arrayOfDicts = new List<Dictionary<string, object?>>
        {
            new Dictionary<string, object?>
            {
                { "text", "a message text" },
                { "type", "text" }
            },
            new Dictionary<string, object?>
            {
                { "text", "another message" },
                { "type", "text" }
            }
        };
        
        var expected = "\"x\":[{\"text\":\"a message text\",\"type\":\"text\"},{\"text\":\"another message\",\"type\":\"text\"}]";
        
        var (writer, readFunc) = CreateUtf8JsonWriter();
        DataItemJsonConverter.WriteFieldValue(writer, "x", arrayOfDicts, includeNullFields: includeNullFields);
        Assert.AreEqual(expected, readFunc(), $"includeNullFields: {includeNullFields}");
    }

    [TestMethod]
    public void Test_AsJsonString_CompleteMongoScenario()
    {
        // Test complete scenario from the issue: nested _id object and array of content dictionaries
        var mongoStyleDoc = new DictionaryDataItem(new Dictionary<string, object?>
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
        });

        var expected = "{\"_id\":{\"$oid\":\"some_id\"},\"thread_id\":\"thread_id\",\"content\":[{\"text\":\"a message text\",\"type\":\"text\"}],\"role\":\"user\"}";
        var json = DataItemJsonConverter.AsJsonString(mongoStyleDoc, false, false);
        Assert.AreEqual(expected, json);
    }
}

