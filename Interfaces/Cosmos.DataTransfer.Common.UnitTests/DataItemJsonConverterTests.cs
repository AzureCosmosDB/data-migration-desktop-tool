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

    // ----------------------------------------------------------------------
    // Multi-dimensional array support (issue #237 / PR #238)
    // ----------------------------------------------------------------------

    [TestMethod]
    public void Test_WriteFieldValue_GeoJsonPoint_Coordinates_OneDimensional()
    {
        // Regression guard: a flat numeric array must continue to serialize correctly.
        var coordinates = new object?[] { 5.347494076316281, 52.033503157065155 };
        var expected = "\"coordinates\":[5.347494076316281,52.033503157065155]";

        var (writer, readFunc) = CreateUtf8JsonWriter();
        DataItemJsonConverter.WriteFieldValue(writer, "coordinates", coordinates, includeNullFields: false);
        Assert.AreEqual(expected, readFunc());
    }

    [TestMethod]
    public void Test_WriteFieldValue_GeoJsonLineString_Coordinates_TwoDimensional()
    {
        // GeoJSON LineString coordinates: array of [x, y] pairs.
        var coordinates = new object?[]
        {
            new object?[] { 5.3474399338950604, 52.03355740411766 },
            new object?[] { 5.347590198744001, 52.033439655450024 }
        };
        var expected = "\"coordinates\":[[5.3474399338950604,52.03355740411766],[5.347590198744001,52.033439655450024]]";

        var (writer, readFunc) = CreateUtf8JsonWriter();
        DataItemJsonConverter.WriteFieldValue(writer, "coordinates", coordinates, includeNullFields: false);
        Assert.AreEqual(expected, readFunc());
    }

    [TestMethod]
    public void Test_WriteFieldValue_GeoJsonPolygon_Coordinates_ThreeDimensional()
    {
        // GeoJSON Polygon coordinates: array of linear rings, each a list of [x, y] pairs.
        var coordinates = new object?[]
        {
            new object?[]
            {
                new object?[] { 5.347432321215393, 52.03355800306437 },
                new object?[] { 5.347432321215393, 52.03343276598602 },
                new object?[] { 5.347605671445933, 52.03343276598602 },
                new object?[] { 5.347605671445933, 52.03355800306437 },
                new object?[] { 5.347432321215393, 52.03355800306437 }
            }
        };
        var expected = "\"coordinates\":[[" +
            "[5.347432321215393,52.03355800306437]," +
            "[5.347432321215393,52.03343276598602]," +
            "[5.347605671445933,52.03343276598602]," +
            "[5.347605671445933,52.03355800306437]," +
            "[5.347432321215393,52.03355800306437]" +
            "]]";

        var (writer, readFunc) = CreateUtf8JsonWriter();
        DataItemJsonConverter.WriteFieldValue(writer, "coordinates", coordinates, includeNullFields: false);
        Assert.AreEqual(expected, readFunc());
    }

    [TestMethod]
    public void Test_AsJsonString_FullGeoJsonDocument()
    {
        // Reproduces the exact failing scenario from issue #237.
        var doc = new DictionaryDataItem(new Dictionary<string, object?>
        {
            { "id", "1" },
            { "name", "Example" },
            { "point", new DictionaryDataItem(new Dictionary<string, object?>
                {
                    { "type", "Point" },
                    { "coordinates", new object?[] { 5.347494076316281, 52.033503157065155 } }
                })
            },
            { "polygon", new DictionaryDataItem(new Dictionary<string, object?>
                {
                    { "type", "Polygon" },
                    { "coordinates", new object?[]
                        {
                            new object?[]
                            {
                                new object?[] { 5.347432321215393, 52.03355800306437 },
                                new object?[] { 5.347432321215393, 52.03343276598602 },
                                new object?[] { 5.347605671445933, 52.03343276598602 },
                                new object?[] { 5.347605671445933, 52.03355800306437 },
                                new object?[] { 5.347432321215393, 52.03355800306437 }
                            }
                        }
                    }
                })
            },
            { "line", new DictionaryDataItem(new Dictionary<string, object?>
                {
                    { "type", "LineString" },
                    { "coordinates", new object?[]
                        {
                            new object?[] { 5.3474399338950604, 52.03355740411766 },
                            new object?[] { 5.347590198744001, 52.033439655450024 }
                        }
                    }
                })
            }
        });

        var json = DataItemJsonConverter.AsJsonString(doc, indented: false, includeNullFields: false);

        // Round-trip back through System.Text.Json to confirm the output is well-formed and
        // structurally matches the input (depth-3 array survives).
        using var parsed = JsonDocument.Parse(json);
        var polygonCoords = parsed.RootElement
            .GetProperty("polygon")
            .GetProperty("coordinates");
        Assert.AreEqual(JsonValueKind.Array, polygonCoords.ValueKind);
        Assert.AreEqual(1, polygonCoords.GetArrayLength()); // 1 linear ring
        var ring = polygonCoords[0];
        Assert.AreEqual(JsonValueKind.Array, ring.ValueKind);
        Assert.AreEqual(5, ring.GetArrayLength()); // 5 points
        var firstPoint = ring[0];
        Assert.AreEqual(JsonValueKind.Array, firstPoint.ValueKind);
        Assert.AreEqual(2, firstPoint.GetArrayLength());
        Assert.AreEqual(5.347432321215393, firstPoint[0].GetDouble());
        Assert.AreEqual(52.03355800306437, firstPoint[1].GetDouble());

        // Critical regression assertion: no more "System.Collections.Generic.List`1[System.Object]".
        StringAssert.DoesNotMatch(json, new Regex("System\\.Collections"));
    }

    [TestMethod]
    public void Test_WriteFieldValue_EmptyNestedArray()
    {
        var value = new object?[] { new object?[] { } };
        var expected = "\"x\":[[]]";

        var (writer, readFunc) = CreateUtf8JsonWriter();
        DataItemJsonConverter.WriteFieldValue(writer, "x", value, includeNullFields: false);
        Assert.AreEqual(expected, readFunc());
    }

    [TestMethod]
    public void Test_WriteFieldValue_NestedArrayWithNulls()
    {
        var value = new object?[]
        {
            new object?[] { null, 1L },
            new object?[] { null }
        };
        var expected = "\"x\":[[null,1],[null]]";

        var (writer, readFunc) = CreateUtf8JsonWriter();
        DataItemJsonConverter.WriteFieldValue(writer, "x", value, includeNullFields: false);
        Assert.AreEqual(expected, readFunc());
    }

    [TestMethod]
    public void Test_WriteFieldValue_NestedArrayMixedScalars()
    {
        var value = new object?[]
        {
            new object?[] { 1L, "a", true },
            new object?[] { 2.5, null }
        };
        var expected = "\"x\":[[1,\"a\",true],[2.5,null]]";

        var (writer, readFunc) = CreateUtf8JsonWriter();
        DataItemJsonConverter.WriteFieldValue(writer, "x", value, includeNullFields: false);
        Assert.AreEqual(expected, readFunc());
    }

    [TestMethod]
    public void Test_WriteFieldValue_NestedArrayOfIDataItems()
    {
        // Mix nested arrays with IDataItem entries to exercise the array-item dispatcher.
        var value = new object?[]
        {
            new object?[]
            {
                new DictionaryDataItem(new Dictionary<string, object?> { { "k", 1L } }),
                new DictionaryDataItem(new Dictionary<string, object?> { { "k", 2L } })
            },
            new object?[]
            {
                new DictionaryDataItem(new Dictionary<string, object?> { { "k", 3L } })
            }
        };
        var expected = "\"x\":[[{\"k\":1},{\"k\":2}],[{\"k\":3}]]";

        var (writer, readFunc) = CreateUtf8JsonWriter();
        DataItemJsonConverter.WriteFieldValue(writer, "x", value, includeNullFields: false);
        Assert.AreEqual(expected, readFunc());
    }

    [TestMethod]
    public void Test_WriteFieldValue_NestedArrayOfDictionaries()
    {
        // Array of arrays of Dictionary<string, object?> (BSON-style nested arrays).
        var value = new object?[]
        {
            new object?[]
            {
                new Dictionary<string, object?> { { "k", "v1" } },
                new Dictionary<string, object?> { { "k", "v2" } }
            }
        };
        var expected = "\"x\":[[{\"k\":\"v1\"},{\"k\":\"v2\"}]]";

        var (writer, readFunc) = CreateUtf8JsonWriter();
        DataItemJsonConverter.WriteFieldValue(writer, "x", value, includeNullFields: false);
        Assert.AreEqual(expected, readFunc());
    }

    [TestMethod]
    public void Test_WriteFieldValue_WideNestedArray_SmokeTest()
    {
        // Performance / correctness smoke: 200 inner arrays, each with 10 numbers.
        const int outerCount = 200;
        const int innerCount = 10;
        var value = new object?[outerCount];
        for (int i = 0; i < outerCount; i++)
        {
            var inner = new object?[innerCount];
            for (int j = 0; j < innerCount; j++)
            {
                inner[j] = (long)(i * innerCount + j);
            }
            value[i] = inner;
        }

        var (writer, readFunc) = CreateUtf8JsonWriter();
        DataItemJsonConverter.WriteFieldValue(writer, "x", value, includeNullFields: false);
        var actual = readFunc();

        // Validate via round-trip rather than building a giant expected string.
        using var parsed = JsonDocument.Parse("{" + actual + "}");
        var arr = parsed.RootElement.GetProperty("x");
        Assert.AreEqual(outerCount, arr.GetArrayLength());
        for (int i = 0; i < outerCount; i++)
        {
            Assert.AreEqual(innerCount, arr[i].GetArrayLength());
            Assert.AreEqual((long)(i * innerCount), arr[i][0].GetInt64());
        }
    }

    [TestMethod]
    public void Test_WriteFieldValue_EmptyStringPropertyName_StillEmitsAsField()
    {
        // Verifies that the refactor did NOT introduce a sentinel: an empty-string field name
        // continues to emit a normal "" property — it is not silently swallowed.
        var (writer, readFunc) = CreateUtf8JsonWriter();
        DataItemJsonConverter.WriteFieldValue(writer, "", 1L, includeNullFields: false);
        Assert.AreEqual("\"\":1", readFunc());
    }

    [TestMethod]
    public void Test_WriteFieldValue_WhitespacePropertyName_StillEmitsAsField()
    {
        // Verifies whitespace property names are preserved verbatim (no IsNullOrWhiteSpace sentinel).
        var (writer, readFunc) = CreateUtf8JsonWriter();
        DataItemJsonConverter.WriteFieldValue(writer, " ", 1L, includeNullFields: false);
        Assert.AreEqual("\" \":1", readFunc());
    }

    [TestMethod]
    public void Test_RoundTrip_GeoJsonViaDeserializeAndAsJsonString()
    {
        // Deserialize the issue-237 source JSON, re-serialize via AsJsonString, and parse the
        // result. The shape (and key numeric values) must be preserved across the round-trip.
        const string sourceJson = """
        {
          "id": "1",
          "name": "Example",
          "polygon": {
            "type": "Polygon",
            "coordinates": [
              [
                [5.347432321215393, 52.03355800306437],
                [5.347432321215393, 52.03343276598602],
                [5.347605671445933, 52.03343276598602],
                [5.347605671445933, 52.03355800306437],
                [5.347432321215393, 52.03355800306437]
              ]
            ]
          }
        }
        """;

        var deserialized = DataItemJsonConverter.Deserialize(sourceJson) as Cosmos.DataTransfer.Interfaces.IDataItem;
        Assert.IsNotNull(deserialized);

        var roundTripped = DataItemJsonConverter.AsJsonString(deserialized, indented: false, includeNullFields: false);
        using var parsed = JsonDocument.Parse(roundTripped);
        var ring = parsed.RootElement.GetProperty("polygon").GetProperty("coordinates")[0];
        Assert.AreEqual(5, ring.GetArrayLength());
        Assert.AreEqual(5.347432321215393, ring[0][0].GetDouble());
        Assert.AreEqual(52.03355800306437, ring[0][1].GetDouble());
        StringAssert.DoesNotMatch(roundTripped, new Regex("System\\.Collections"));
    }

    // ----------------------------------------------------------------------
    // Depth-guard tests (stack-safety against pathological / recursive input)
    // ----------------------------------------------------------------------

    /// <summary>
    /// Builds an array nested exactly <paramref name="depth"/> levels deep. The innermost array
    /// contains a single long value. Depth 1 means a single flat array <c>[1]</c>.
    /// </summary>
    private static object?[] BuildNestedArray(int depth)
    {
        if (depth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(depth));
        }

        object?[] current = new object?[] { 1L };
        for (int i = 1; i < depth; i++)
        {
            current = new object?[] { current };
        }
        return current;
    }

    /// <summary>
    /// Builds a chain of nested <see cref="DictionaryDataItem"/>s exactly <paramref name="levels"/>
    /// deep. Uses an iterative build to avoid blowing the helper's own stack during test setup.
    /// </summary>
    private static DictionaryDataItem BuildNestedDataItem(int levels)
    {
        if (levels < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(levels));
        }
        var current = new DictionaryDataItem(new Dictionary<string, object?> { { "leaf", 1L } });
        for (int i = 1; i < levels; i++)
        {
            current = new DictionaryDataItem(new Dictionary<string, object?> { { "n", current } });
        }
        return current;
    }

    [TestMethod]
    public void Test_WriteFieldValue_DeeplyNestedArray_AtMaxDepth_Succeeds()
    {
        // Build an array whose nesting matches the limit exactly. Should serialize without throwing.
        var nested = BuildNestedArray(DataItemJsonConverter.MaxJsonDepth);
        var (writer, readFunc) = CreateUtf8JsonWriter();
        DataItemJsonConverter.WriteFieldValue(writer, "x", nested, includeNullFields: false);

        var actual = readFunc();
        // Sanity: count the opening brackets — should equal MaxJsonDepth.
        int openBrackets = 0;
        foreach (var ch in actual)
        {
            if (ch == '[')
            {
                openBrackets++;
            }
        }
        Assert.AreEqual(DataItemJsonConverter.MaxJsonDepth, openBrackets);
    }

    [TestMethod]
    public void Test_WriteFieldValue_DeeplyNestedArray_OverMaxDepth_Throws()
    {
        var nested = BuildNestedArray(DataItemJsonConverter.MaxJsonDepth + 1);
        var (writer, _) = CreateUtf8JsonWriter();
        var ex = Assert.ThrowsException<InvalidOperationException>(() =>
            DataItemJsonConverter.WriteFieldValue(writer, "x", nested, includeNullFields: false));
        StringAssert.Contains(ex.Message, "nesting depth");
    }

    [TestMethod]
    public void Test_WriteFieldValue_DeeplyNestedDataItems_OverMaxDepth_Throws()
    {
        // Object-nesting variant of the depth-guard test: build a chain of nested
        // DictionaryDataItems and confirm the same exception fires on overflow.
        // Going through WriteFieldValue (rather than AsJsonString directly) keeps depth
        // semantics symmetric with the array-nesting tests above.
        var oversized = BuildNestedDataItem(DataItemJsonConverter.MaxJsonDepth + 1);
        var (writer, _) = CreateUtf8JsonWriter();
        var ex = Assert.ThrowsException<InvalidOperationException>(() =>
            DataItemJsonConverter.WriteFieldValue(writer, "x", oversized, includeNullFields: false));
        StringAssert.Contains(ex.Message, "nesting depth");
    }

    [TestMethod]
    public void Test_WriteFieldValue_DeeplyNestedDataItems_AtMaxDepth_Succeeds()
    {
        // At-the-limit case for object nesting via WriteFieldValue.
        var sized = BuildNestedDataItem(DataItemJsonConverter.MaxJsonDepth);
        var (writer, readFunc) = CreateUtf8JsonWriter();
        DataItemJsonConverter.WriteFieldValue(writer, "x", sized, includeNullFields: false);

        var actual = readFunc();
        int openBraces = 0;
        foreach (var ch in actual)
        {
            if (ch == '{')
            {
                openBraces++;
            }
        }
        Assert.AreEqual(DataItemJsonConverter.MaxJsonDepth, openBraces);
    }

    [TestMethod]
    public void Test_WriteFieldValue_MixedObjectAndArrayDepth_OverMaxDepth_Throws()
    {
        // Alternate object / array nesting and confirm the depth counter covers both kinds.
        // Build pairs of (array, object) so each iteration adds 2 levels; overshoot the limit.
        object? current = 1L;
        int pairs = (DataItemJsonConverter.MaxJsonDepth / 2) + 2;
        for (int i = 0; i < pairs; i++)
        {
            current = new DictionaryDataItem(new Dictionary<string, object?> { { "n", current } });
            current = new object?[] { current };
        }

        var (writer, _) = CreateUtf8JsonWriter();
        var ex = Assert.ThrowsException<InvalidOperationException>(() =>
            DataItemJsonConverter.WriteFieldValue(writer, "x", current, includeNullFields: false));
        StringAssert.Contains(ex.Message, "nesting depth");
    }
}

