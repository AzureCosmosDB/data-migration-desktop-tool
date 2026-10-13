using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cosmos.DataTransfer.JsonExtension.UnitTests;

[TestClass]
public class JsonArrayDiagnosticsTests
{
    public TestContext TestContext { get; set; } = null!;

    [DataTestMethod]
    [DataRow(0, int.MaxValue)]
    [DataRow(2, 1)]
    [DataRow(10000, 1)]
    [DataRow(10000, int.MaxValue)]
    [DataRow(10000, 97)]
    [DataRow(10000, 4096)]
    [DataRow(10000, 16383)]
    [DataRow(10000, 16384)]
    [DataRow(10000, 16385)]
    public async Task ParseDataAsync_MalformedArrayItem_ReportsGlobalIndex(int validCount, int maxReadSize)
    {
        TestContext.WriteLine($"Runtime: {Environment.Version}; JSON assembly: {typeof(JsonSerializer).Assembly.Location}");
        TestContext.WriteLine($"JSON version: {typeof(JsonSerializer).Assembly.GetCustomAttributesData().First(a => a.AttributeType.Name == "AssemblyInformationalVersionAttribute").ConstructorArguments[0]}");
        var json = "[" + string.Concat(Enumerable.Repeat("{\"id\":1},", validCount)) + "{\"name\":}]";
        var bytes = Encoding.UTF8.GetBytes(json);
        var expected = Assert.ThrowsException<JsonException>(() =>
            JsonSerializer.Deserialize<Dictionary<string, object?>[]>(bytes));
        using var stream = new ChunkedReadStream(bytes, maxReadSize);
        int emitted = 0;
        var error = await Assert.ThrowsExceptionAsync<JsonException>(async () =>
        {
            await foreach (var item in ReadItems(stream))
                emitted++;
        });

        Assert.AreEqual($"$[{validCount}].name", error.Path);
        Assert.AreEqual(expected.Path, error.Path);
        Assert.AreEqual(expected.LineNumber, error.LineNumber);
        Assert.AreEqual(expected.BytePositionInLine, error.BytePositionInLine);
        if (emitted > 0)
        {
            Assert.IsInstanceOfType<JsonException>(error.InnerException);
            var original = (JsonException)error.InnerException;
            Assert.AreEqual(original.LineNumber, error.LineNumber);
            Assert.AreEqual(original.BytePositionInLine, error.BytePositionInLine);
            StringAssert.Contains(error.Message, $"Path: $[{validCount}].name");
        }
        TestContext.WriteLine($"Emitted: {emitted}; path: {error.Path}; line: {error.LineNumber}; byte: {error.BytePositionInLine}");
    }

    [DataTestMethod]
    [DataRow(0, 1)]
    [DataRow(2, 97)]
    [DataRow(10000, 97)]
    [DataRow(10000, 16384)]
    public async Task ParseDataAsync_MalformedValues_PreservesParserPathAndLocation(int validCount, int maxReadSize)
    {
        string[] endings =
        [
            "{\"name\":}]",
            "{\"name\":tru}]",
            "{\"name\":\"unterminated",
            "{\"name\":1e}]",
            "{\"a.b[2]\":}]",
            "{\"parent\":{\"children\":[{\"name\":}]}}]",
            "{\"parent\":[1,2,]}]",
            "42]",
            "\"not an object\"]",
            "true]",
            "[]]",
            ",]",
            "{\"name\":1,}]",
            "{\"name\":1}",
            "/*comment*/{}]",
        ];
        // Include null entries, CRLFs, and multibyte UTF-8 before the failure.
        var prefix = string.Concat(Enumerable.Range(0, validCount)
            .Select(i => i % 2 == 0 ? "null,\r\n" : "{\"text\":\"\u00e9\"},\r\n"));
        foreach (string ending in endings)
        {
            var bytes = Encoding.UTF8.GetBytes("[\r\n" + prefix + ending);
            using var fullInput = new MemoryStream(bytes);
            var expected = Assert.ThrowsException<JsonException>(() =>
                JsonSerializer.Deserialize<Dictionary<string, object?>[]>(fullInput));
            using var stream = new ChunkedReadStream(bytes, maxReadSize);
            var error = await Assert.ThrowsExceptionAsync<JsonException>(async () =>
                await ReadItems(stream).ToListAsync(), ending);

            Assert.AreEqual(expected.Path, error.Path, ending);
            Assert.AreEqual(expected.LineNumber, error.LineNumber, ending);
            Assert.AreEqual(expected.BytePositionInLine, error.BytePositionInLine, ending);
            if (error.InnerException is JsonException original)
            {
                Assert.AreEqual(original.LineNumber, error.LineNumber);
                Assert.AreEqual(original.BytePositionInLine, error.BytePositionInLine);
                StringAssert.Contains(error.Message, $"Path: {error.Path}");
                StringAssert.Contains(error.Message, $"LineNumber: {error.LineNumber}");
                StringAssert.Contains(error.Message, $"BytePositionInLine: {error.BytePositionInLine}");
            }
        }
    }

    [DataTestMethod]
    [DataRow(0, 1)]
    [DataRow(10000, 97)]
    [DataRow(10000, 16384)]
    public async Task ParseDataAsync_ErrorOutsideArray_PreservesRootPath(int validCount, int maxReadSize)
    {
        var json = "[" + string.Join(",", Enumerable.Repeat("{}", validCount)) + "] false";
        var bytes = Encoding.UTF8.GetBytes(json);
        using var stockStream = new ChunkedReadStream(bytes, maxReadSize);
        var expected = await Assert.ThrowsExceptionAsync<JsonException>(async () =>
            await JsonSerializer.DeserializeAsyncEnumerable<Dictionary<string, object?>>(stockStream).ToListAsync());
        using var stream = new ChunkedReadStream(bytes, maxReadSize);
        var error = await Assert.ThrowsExceptionAsync<JsonException>(async () =>
            await ReadItems(stream).ToListAsync());

        Assert.AreEqual("$", error.Path);
        Assert.AreEqual(expected.Path, error.Path);
        Assert.AreEqual(expected.LineNumber, error.LineNumber);
        Assert.AreEqual(expected.BytePositionInLine, error.BytePositionInLine);
        Assert.AreEqual(expected.Message, error.Message);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("false")]
    [DataRow("42")]
    public async Task ParseDataAsync_InvalidArrayRoot_DoesNotInventIndex(string json)
    {
        using var stream = new ChunkedReadStream(Encoding.UTF8.GetBytes(json), 1);
        using var stockStream = new ChunkedReadStream(Encoding.UTF8.GetBytes(json), 1);
        var expected = await Assert.ThrowsExceptionAsync<JsonException>(async () =>
            await JsonSerializer.DeserializeAsyncEnumerable<Dictionary<string, object?>>(stockStream).ToListAsync());
        var error = await Assert.ThrowsExceptionAsync<JsonException>(async () =>
            await ReadItems(stream).ToListAsync());
        Assert.AreEqual("$", error.Path);
        Assert.AreEqual(expected.Message, error.Message);
    }

    [TestMethod]
    public async Task ParseDataAsync_NewStream_ResetsIndex()
    {
        using var first = new ChunkedReadStream(Encoding.UTF8.GetBytes("[{},null,{}]"), 1);
        using var second = new ChunkedReadStream(Encoding.UTF8.GetBytes("[null,{},null,{\"name\":}]"), 1);
        var error = await Assert.ThrowsExceptionAsync<JsonException>(async () =>
            await new JsonFormatReader().ParseDataAsync(new StreamSource(first, second),
                new ConfigurationBuilder().Build(), NullLogger.Instance).ToListAsync());
        Assert.AreEqual("$[3].name", error.Path);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ParseDataAsync_SeekableOrCompressedArray_PreservesGlobalIndex(bool compressed)
    {
        var json = "[\r\n" + string.Concat(Enumerable.Repeat("{\"id\":1},\r\n", 10000)) + "{\"name\":}]";
        byte[] bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(json)).ToArray();
        using var fullInput = new MemoryStream(bytes);
        var expected = Assert.ThrowsException<JsonException>(() =>
            JsonSerializer.Deserialize<Dictionary<string, object?>[]>(fullInput));
        using var input = new MemoryStream(bytes);
        using var compressedInput = new MemoryStream();
        using (var compressor = new GZipStream(compressedInput, CompressionMode.Compress, leaveOpen: true))
            compressor.Write(bytes);
        compressedInput.Position = 0;
        using var decompressor = new GZipStream(compressedInput, CompressionMode.Decompress);
        var error = await Assert.ThrowsExceptionAsync<JsonException>(async () =>
            await ReadItems(compressed ? decompressor : input).ToListAsync());

        Assert.AreEqual("$[10000].name", error.Path);
        Assert.AreEqual(expected.LineNumber, error.LineNumber);
        Assert.AreEqual(expected.BytePositionInLine, error.BytePositionInLine);
    }

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(97)]
    [DataRow(16384)]
    [DataRow(int.MaxValue)]
    public async Task ParseDataAsync_LargeValidArray_StreamsInOrderWithNumericFidelity(int maxReadSize)
    {
        var json = "[" + string.Join(",", Enumerable.Range(0, 10000).Select(i =>
            $"{{\"id\":{i},\"integer\":9223372036854775807,\"number\":1.234567890123456789,\"child\":{{\"values\":[null,true,2]}}}},null")) + "]";
        var bytes = Encoding.UTF8.GetBytes(json);
        using var stream = new ChunkedReadStream(bytes, maxReadSize);
        int count = 0;
        await foreach (var item in ReadItems(stream))
        {
            Assert.AreEqual(count++, item.GetValue("id"));
            Assert.AreEqual(long.MaxValue, item.GetValue("integer"));
            var dictionary = (JsonDictionaryDataItem)item;
            Assert.AreEqual("1.234567890123456789", ((JsonElement)dictionary.Items["number"]!).GetRawText());
            Assert.IsInstanceOfType<JsonDictionaryDataItem>(item.GetValue("child"));
            if (count == 1)
                Assert.IsTrue(stream.BytesRead < bytes.Length, "The first item must be yielded before the entire array is read.");
        }
        Assert.AreEqual(10000, count);
    }

    [TestMethod]
    public async Task ParseDataAsync_ItemSpanningManyBuffers_PreservesGlobalIndex()
    {
        var json = "[" + string.Concat(Enumerable.Repeat("{},", 10000)) +
            "{\"padding\":\"" + new string('x', 100000) + "\",\"a.b\":}]";
        var bytes = Encoding.UTF8.GetBytes(json);
        var expected = Assert.ThrowsException<JsonException>(() =>
            JsonSerializer.Deserialize<Dictionary<string, object?>[]>(bytes));
        using var stream = new ChunkedReadStream(bytes, 97);
        var error = await Assert.ThrowsExceptionAsync<JsonException>(async () =>
            await ReadItems(stream).ToListAsync());
        Assert.AreEqual("$[10000]['a.b']", error.Path);
        Assert.AreEqual(expected.BytePositionInLine, error.BytePositionInLine);
    }

    [DataTestMethod]
    [DataRow("[]", 0)]
    [DataRow("[null,{},null]", 1)]
    [DataRow("{\"id\":1}", 1)]
    public async Task ParseDataAsync_ValidInput_PreservesBehavior(string json, int expectedCount)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var items = await ReadItems(stream).ToListAsync();
        Assert.AreEqual(expectedCount, items.Count);
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task ParseDataAsync_Cancellation_IsNotWrapped(bool cancelBeforeReading)
    {
        using var cancellation = new CancellationTokenSource();
        using var stream = new ChunkedReadStream(
            Encoding.UTF8.GetBytes("[" + string.Join(",", Enumerable.Repeat("{}", 10000)) + "]"), 97);
        if (cancelBeforeReading)
            cancellation.Cancel();
        try
        {
            await foreach (var item in ReadItems(stream, cancellation.Token))
                cancellation.Cancel();
            Assert.Fail("Expected cancellation.");
        }
        catch (OperationCanceledException ex)
        {
            Assert.AreEqual(cancellation.Token, ex.CancellationToken);
        }
        Assert.IsTrue(stream.BytesRead < 30001);
        Assert.IsTrue(stream.IsDisposed);
    }

    [TestMethod]
    public async Task ParseDataAsync_EarlyStop_DisposesSourceEnumeratorWithoutReadingToEnd()
    {
        var bytes = Encoding.UTF8.GetBytes("[" + string.Join(",", Enumerable.Repeat("{}", 10000)) + "]");
        using var stream = new ChunkedReadStream(bytes, 97);
        await using (var items = ReadItems(stream).GetAsyncEnumerator())
            Assert.IsTrue(await items.MoveNextAsync());

        Assert.IsTrue(stream.BytesRead < bytes.Length);
        Assert.IsTrue(stream.IsDisposed);
    }

    [TestMethod]
    public async Task ParseDataAsync_StreamFailure_PreservesOriginalException()
    {
        var expected = new IOException("Synthetic read failure.");
        using var stream = new ChunkedReadStream(Encoding.UTF8.GetBytes("[{},{},{}]"), 4, expected);
        var error = await Assert.ThrowsExceptionAsync<IOException>(async () =>
            await ReadItems(stream).ToListAsync());
        Assert.AreSame(expected, error);
        Assert.IsTrue(stream.IsDisposed);
    }

    private static IAsyncEnumerable<IDataItem> ReadItems(Stream stream, CancellationToken cancellationToken = default)
    {
        return new JsonFormatReader().ParseDataAsync(
            new StreamSource(stream), new ConfigurationBuilder().Build(), NullLogger.Instance, cancellationToken);
    }

    private sealed class StreamSource(params Stream[] streams) : IComposableDataSource
    {
        public async IAsyncEnumerable<Stream?> ReadSourceAsync(
            IConfiguration config, ILogger logger, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var stream in streams)
            {
                using (stream)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return stream;
                }
            }
            await Task.CompletedTask;
        }

        public IEnumerable<IDataExtensionSettings> GetSettings() => [];
    }

    private sealed class ChunkedReadStream(byte[] bytes, int maxReadSize, IOException? readError = null) : Stream
    {
        private readonly MemoryStream inner = new(bytes);
        public long BytesRead { get; private set; }
        public bool IsDisposed { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = inner.Read(buffer, offset, Math.Min(count, maxReadSize));
            BytesRead += read;
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (readError != null && BytesRead >= maxReadSize)
                throw readError;
            int read = await inner.ReadAsync(buffer[..Math.Min(buffer.Length, maxReadSize)], cancellationToken);
            BytesRead += read;
            return read;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
                IsDisposed = true;
            }
            base.Dispose(disposing);
        }
    }
}
