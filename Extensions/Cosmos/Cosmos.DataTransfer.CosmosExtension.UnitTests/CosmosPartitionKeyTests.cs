using System.Dynamic;
using System.Globalization;
using System.Net;
using System.Reflection;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Azure.Cosmos;
using Moq;
using Newtonsoft.Json;

namespace Cosmos.DataTransfer.CosmosExtension.UnitTests;

[TestClass]
public class CosmosPartitionKeyTests
{
    public static IEnumerable<object[]> PartitionKeyCases() =>
        PartitionKeyValues().Select(row => new[] { row[0], row[1], row[2], row[3].ToString()! });

    private static IEnumerable<object[]> PartitionKeyValues()
    {
        yield return new object[] { "missing", "{}", "/pk", PartitionKey.None };
        yield return new object[] { "null", """{"pk":null}""", "/pk", PartitionKey.Null };
        yield return new object[] { "string", """{"pk":"example"}""", "/pk", new PartitionKey("example") };
        yield return new object[] { "empty string", """{"pk":""}""", "/pk", new PartitionKey("") };
        yield return new object[] { "integer", """{"pk":42}""", "/pk", new PartitionKey(42) };
        yield return new object[] { "large integer", """{"pk":638676324052177500}""", "/pk", new PartitionKey((double)638676324052177500L) };
        yield return new object[] { "fraction", """{"pk":-12.75}""", "/pk", new PartitionKey(-12.75) };
        yield return new object[] { "true", """{"pk":true}""", "/pk", new PartitionKey(true) };
        yield return new object[] { "false", """{"pk":false}""", "/pk", new PartitionKey(false) };
        yield return new object[] { "nested value", """{"parent":{"pk":42}}""", "/parent/pk", new PartitionKey(42) };
        yield return new object[] { "nested missing", """{"parent":{}}""", "/parent/pk", PartitionKey.None };
        yield return new object[] { "nested null", """{"parent":{"pk":null}}""", "/parent/pk", PartitionKey.Null };
        yield return new object[] { "null parent", """{"parent":null}""", "/parent/pk", PartitionKey.None };
        yield return new object[] { "scalar parent", """{"parent":42}""", "/parent/pk", PartitionKey.None };
        yield return new object[] { "date string", """{"pk":"2023-12-19T00:00:00.000Z"}""", "/pk", new PartitionKey("2023-12-19T00:00:00.000Z") };
        yield return new object[] { "case sensitive", """{"PK":"example"}""", "/pk", PartitionKey.None };
    }

    [DataTestMethod]
    [DynamicData(nameof(PartitionKeyCases), DynamicDataSourceType.Method)]
    public async Task PopulateItem_PreservesPartitionKeyType(string scenario, string json, string path, string expected)
    {
        var item = JsonConvert.DeserializeObject<ExpandoObject>(json, RawJsonCosmosSerializer.GetDefaultSettings())!;
        ((IDictionary<string, object?>)item)["id"] = "item-id";
        foreach (var mode in ExplicitKeyModes)
        {
            await AssertRequest(item, new[] { path }, expected, mode, scenario);
        }
    }

    private static readonly DataWriteMode[] ExplicitKeyModes =
    {
        DataWriteMode.InsertStream, DataWriteMode.UpsertStream, DataWriteMode.DeleteStream, DataWriteMode.Delete
    };

    [TestMethod]
    public async Task PopulateItem_UsesQuotedPathSegmentsAcrossAllExplicitKeyModes()
    {
        var item = new ExpandoObject();
        var values = (IDictionary<string, object?>)item;
        values["tenant/key"] = "literal";
        values[" spaced "] = true;
        values["escaped\\\"quote"] = 42;
        values[""] = "empty";
        var child = new ExpandoObject();
        ((IDictionary<string, object?>)child)["region/name"] = "west";
        values["parent"] = child;
        var cases = new[]
        {
            (new[] { "/\"tenant/key\"" }, new PartitionKey("literal")),
            (new[] { "/'tenant/key'" }, new PartitionKey("literal")),
            (new[] { "/\" spaced \"" }, new PartitionKey(true)),
            (new[] { "/\"escaped\\\"quote\"" }, new PartitionKey(42)),
            (new[] { "/\"\"" }, new PartitionKey("empty")),
            (new[] { "//" }, new PartitionKey("empty")),
            (new[] { "/ parent /'region/name'/" }, new PartitionKey("west")),
            (new[] { "/\"tenant/key\"", "/parent/\"region/name\"", "/'absent/field'" },
                new PartitionKeyBuilder().Add("literal").Add("west").AddNoneType().Build())
        };
        foreach (var (paths, expected) in cases)
        {
            foreach (var mode in ExplicitKeyModes)
                await AssertRequest(item, paths, expected.ToString(), mode, "quoted path");
        }
    }

    [DataTestMethod]
    [DataRow("/\"unterminated")]
    [DataRow("/'unterminated")]
    [DataRow("/\"tenant/key\"suffix")]
    [DataRow("/absent/\"unterminated")]
    public async Task PopulateItem_RejectsMalformedPathsBeforeSending(string path)
    {
        foreach (var mode in ExplicitKeyModes)
        {
            var container = new Mock<Container>(MockBehavior.Strict);
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => PopulateItem(container.Object, new ExpandoObject(), new[] { path }, mode));
            Assert.AreEqual(0, container.Invocations.Count);
        }
    }

    [DataTestMethod]
    [DataRow(65)]
    [DataRow(128)]
    public async Task PopulateItem_AcceptsSupportedDocumentDepth(int depth)
    {
        var item = CreateDeepItem(depth);
        foreach (var mode in ExplicitKeyModes)
            await AssertRequest(item, new[] { "/pk" }, new PartitionKey("shallow").ToString(), mode, $"depth {depth}");
    }

    [TestMethod]
    public async Task PopulateItem_RejectsDocumentsBeyondSupportedDepth()
    {
        foreach (var mode in ExplicitKeyModes)
        {
            var container = new Mock<Container>(MockBehavior.Strict);
            try
            {
                await PopulateItem(container.Object, CreateDeepItem(129), new[] { "/pk" }, mode);
                Assert.Fail("Documents deeper than 128 must be rejected before sending.");
            }
            catch (System.Text.Json.JsonException)
            {
                // JsonReaderException is internal; assert through its public base type.
            }
            Assert.AreEqual(0, container.Invocations.Count);
        }
    }

    private static ExpandoObject CreateDeepItem(int depth)
    {
        var item = new ExpandoObject();
        ((IDictionary<string, object?>)item)["pk"] = "shallow";
        var current = item;
        for (int level = 1; level < depth; level++)
        {
            var child = new ExpandoObject();
            ((IDictionary<string, object?>)current)["child"] = child;
            current = child;
        }
        ((IDictionary<string, object?>)current)["value"] = true;
        return item;
    }

    [TestMethod]
    public async Task PopulateItem_PreservesHierarchicalOrderAndMissingComponents()
    {
        var cases = new[]
        {
            ("""{"tenant":"one","nested":{"number":42},"active":true}""",
                new PartitionKeyBuilder().Add("one").Add(42).Add(true).Build()),
            ("""{"tenant":"one","nested":{"number":null}}""",
                new PartitionKeyBuilder().Add("one").AddNullValue().AddNoneType().Build()),
            ("""{"active":false}""",
                new PartitionKeyBuilder().AddNoneType().AddNoneType().Add(false).Build()),
            ("{}", new PartitionKeyBuilder().AddNoneType().AddNoneType().AddNoneType().Build())
        };
        foreach (var (json, expected) in cases)
        {
            var item = JsonConvert.DeserializeObject<ExpandoObject>(json)!;
            foreach (var mode in ExplicitKeyModes)
            {
                await AssertRequest(item, new[] { "/tenant", "/nested/number", "/active" }, expected.ToString(), mode, "hierarchical");
            }
        }
    }

    [TestMethod]
    public async Task PopulateItem_UsesSerializedClrValuesRegardlessOfCulture()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var cases = new (object value, PartitionKey expected)[]
            {
                ((byte)42, new PartitionKey(42)),
                (42L, new PartitionKey(42)),
                (12.75m, new PartitionKey(12.75)),
                (12.75f, new PartitionKey(12.75)),
                (12.75d, new PartitionKey(12.75)),
                (DayOfWeek.Friday, new PartitionKey(5)),
                (new DateTime(2023, 12, 19, 0, 0, 0, DateTimeKind.Utc), new PartitionKey("2023-12-19T00:00:00Z")),
                (Guid.Parse("7d63590e-fac6-4c52-a03c-e9d875ed1f8f"), new PartitionKey("7d63590e-fac6-4c52-a03c-e9d875ed1f8f"))
            };
            foreach (var (value, expected) in cases)
            {
                var item = new ExpandoObject();
                ((IDictionary<string, object?>)item)["pk"] = value;
                foreach (var mode in ExplicitKeyModes)
                {
                    await AssertRequest(item, new[] { "/pk" }, expected.ToString(), mode, value.GetType().Name);
                }
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PopulateItem_UsesTransformedAndNullFilteredPayload(bool ignoreNullValues)
    {
        var data = new CosmosDictionaryDataItem(new Dictionary<string, object?>
        {
            ["ID"] = 123,
            ["source"] = "42",
            ["nested"] = new CosmosDictionaryDataItem(new Dictionary<string, object?> { ["nullable"] = null })
        });
        var item = data.BuildDynamicObjectTree(requireStringId: true, ignoreNullValues: ignoreNullValues,
            transformations: new Dictionary<string, DataItemTransformation>
            {
                ["source"] = new() { DestinationFieldName = "pk", DestinationFieldTypeCode = "Int64" }
            })!;
        Assert.AreEqual("123", ((IDictionary<string, object?>)item)["id"]);
        var builder = new PartitionKeyBuilder().Add(42);
        var expected = (ignoreNullValues ? builder.AddNoneType() : builder.AddNullValue()).Build();
        foreach (var mode in ExplicitKeyModes)
        {
            await AssertRequest(item, new[] { "/pk", "/nested/nullable" }, expected.ToString(), mode, "transformed");
        }
    }

    [DataTestMethod]
    [DataRow("""{"pk":{"secret":"do-not-log"}}""")]
    [DataRow("""{"pk":["do-not-log"]}""")]
    public async Task PopulateItem_RejectsComplexKeysWithoutSendingOrExposingPayload(string json)
    {
        var item = JsonConvert.DeserializeObject<ExpandoObject>(json)!;
        foreach (var mode in ExplicitKeyModes)
        {
            var container = new Mock<Container>(MockBehavior.Strict);
            var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => PopulateItem(container.Object, item, new[] { "/pk" }, mode));
            StringAssert.Contains(exception.Message, "/pk");
            Assert.IsFalse(exception.Message.Contains("do-not-log"));
            Assert.AreEqual(0, container.Invocations.Count);
        }
    }

    private static async Task AssertRequest(ExpandoObject item, string[] paths, string expected, DataWriteMode mode, string scenario)
    {
        var container = new Mock<Container>(MockBehavior.Strict);
        PartitionKey? capturedKey = null;
        string? capturedBody = null;
        string? capturedId = null;
        using var cancellation = new CancellationTokenSource();
        using var response = new ResponseMessage(mode is DataWriteMode.Delete or DataWriteMode.DeleteStream
            ? HttpStatusCode.NoContent : HttpStatusCode.Created);

        Task<ResponseMessage> CaptureWrite(Stream stream, PartitionKey key, ItemRequestOptions options, CancellationToken token)
        {
            capturedKey = key;
            using var reader = new StreamReader(stream, leaveOpen: true);
            capturedBody = reader.ReadToEnd();
            Assert.AreEqual(cancellation.Token, token);
            return Task.FromResult(response);
        }

        container.Setup(c => c.CreateItemStreamAsync(It.IsAny<Stream>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), cancellation.Token))
            .Returns(CaptureWrite);
        container.Setup(c => c.UpsertItemStreamAsync(It.IsAny<Stream>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), cancellation.Token))
            .Returns(CaptureWrite);
        container.Setup(c => c.DeleteItemStreamAsync(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), cancellation.Token))
            .Returns((string id, PartitionKey key, ItemRequestOptions options, CancellationToken token) =>
            {
                capturedId = id;
                capturedKey = key;
                return Task.FromResult(response);
            });
        var typedResponse = new Mock<ItemResponse<ExpandoObject>>();
        typedResponse.SetupGet(r => r.StatusCode).Returns(HttpStatusCode.NoContent);
        container.Setup(c => c.DeleteItemAsync<ExpandoObject>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), cancellation.Token))
            .Returns((string id, PartitionKey key, ItemRequestOptions options, CancellationToken token) =>
            {
                capturedId = id;
                capturedKey = key;
                return Task.FromResult(typedResponse.Object);
            });

        var result = await PopulateItem(container.Object, item, paths, mode, cancellation.Token);

        Assert.AreEqual(expected, capturedKey?.ToString(), $"{scenario}, {mode}");
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, container.Invocations.Count);
        if (mode is DataWriteMode.InsertStream or DataWriteMode.UpsertStream)
        {
            Assert.IsNotNull(capturedBody);
            Assert.AreEqual(JsonConvert.SerializeObject(item, RawJsonCosmosSerializer.GetDefaultSettings()),
                capturedBody, $"Payload changed: {scenario}, {mode}");
        }
        else
        {
            Assert.AreEqual("item-id", capturedId);
        }
    }

    private static Task<CosmosDataSinkExtension.ItemResult> PopulateItem(Container container, ExpandoObject item, string[] paths, DataWriteMode mode, CancellationToken token = default)
    {
        var method = typeof(CosmosDataSinkExtension).GetMethod("PopulateItem", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (Task<CosmosDataSinkExtension.ItemResult>)method.Invoke(null, new object?[]
        {
            container, item, paths, mode, "item-id", token
        })!;
    }
}
