using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Cosmos.DataTransfer.Common.UnitTests;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cosmos.DataTransfer.JsonExtension.UnitTests;

[TestClass]
public class JsonFormatReaderTests
{
    private static readonly IConfiguration Config = TestHelpers.CreateConfig(new());

    [DataTestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task ParseDataAsync_InvalidJson_PreservesOriginalLocation(bool array, bool nonSeekable)
    {
        var json = (array ? "[" : "") + "{\n  \"id\": \"private-value\",\n  \"name\": }\n" + (array ? "]" : "");
        var bytes = Encoding.UTF8.GetBytes(json);
        JsonException expected;
        if (array)
        {
            expected = Assert.ThrowsException<JsonException>(() => JsonSerializer.Deserialize<Dictionary<string, object?>[]>(bytes));
        }
        else
        {
            expected = Assert.ThrowsException<JsonException>(() => JsonSerializer.Deserialize<Dictionary<string, object?>>(bytes));
        }

        var stream = new TestStream(bytes, nonSeekable, maxReadSize: 1);
        var logger = new Mock<ILogger>();
        var error = await Assert.ThrowsExceptionAsync<JsonException>(async () =>
            await ReadAsync(stream, logger.Object).ToListAsync());

        Assert.AreEqual(expected.Path, error.Path);
        Assert.AreEqual(expected.LineNumber, error.LineNumber);
        Assert.AreEqual(expected.BytePositionInLine, error.BytePositionInLine);
        StringAssert.Contains(error.Message, "invalid start of a value");
        logger.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("stream 1") && !state.ToString()!.Contains("private-value")),
            error, It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        Assert.IsTrue(stream.Disposed);
    }

    [DataTestMethod]
    [DataRow("{}")]
    [DataRow("[{}]")]
    [DataRow("[]")]
    [DataRow("[null]")]
    [DataRow("null")]
    public async Task ParseDataAsync_ValidJson_WithBomWhitespaceAndShortReads(string json)
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(new string(' ', 20000) + "\r\n\t" + json)).ToArray();
        var stream = new TestStream(bytes, nonSeekable: true, maxReadSize: 2);
        var items = await ReadAsync(stream).ToListAsync();
        Assert.AreEqual(json is "[]" or "[null]" or "null" ? 0 : 1, items.Count);
        Assert.IsTrue(stream.Disposed);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow(" \r\n\t")]
    [DataRow("{\"id\":1,}")]
    [DataRow("[{\"id\":1,}]")]
    [DataRow("{/*comment*/\"id\":1}")]
    [DataRow("{} {}")]
    [DataRow("[] []")]
    [DataRow("42")]
    public async Task ParseDataAsync_InvalidOrUnsupportedJson_Throws(string json)
    {
        var stream = new TestStream(Encoding.UTF8.GetBytes(json), nonSeekable: true);
        await Assert.ThrowsExceptionAsync<JsonException>(async () => await ReadAsync(stream).ToListAsync());
        Assert.IsTrue(stream.Disposed);
    }

    [TestMethod]
    public async Task ParseDataAsync_LargeObject_PreservesNumericValues()
    {
        var json = "{\"value\":638676324052177500,\"text\":\"" + new string('a', 1500000) + "\"}";
        var items = await ReadAsync(new TestStream(Encoding.UTF8.GetBytes(json), nonSeekable: true)).ToListAsync();
        Assert.AreEqual(1, items.Count);
        Assert.AreEqual(638676324052177500L, items[0].GetValue("value"));
        Assert.AreEqual(1500000, ((string)items[0].GetValue("text")!).Length);
    }

    [TestMethod]
    public async Task ParseDataAsync_LargeArray_IsStreamedAndDisposedOnEarlyExit()
    {
        var bytes = Encoding.UTF8.GetBytes("[" + string.Join(",", Enumerable.Repeat("{\"id\":1}", 100000)) + "]");
        var stream = new TestStream(bytes, nonSeekable: true);
        await using (var enumerator = ReadAsync(stream).GetAsyncEnumerator())
        {
            Assert.IsTrue(await enumerator.MoveNextAsync());
            Assert.AreEqual(1, enumerator.Current.GetValue("id"));
            Assert.IsTrue(stream.BytesRead < bytes.Length / 2, $"Read {stream.BytesRead} of {bytes.Length} bytes before first item");
        }
        Assert.IsTrue(stream.Disposed);
    }

    [TestMethod]
    public async Task ParseDataAsync_LateArrayError_IsLoggedAndPropagated()
    {
        var json = "[" + string.Join(",", Enumerable.Repeat("{\"id\":1}", 10000)) + ",{\"name\":}]";
        var stream = new TestStream(Encoding.UTF8.GetBytes(json), nonSeekable: true);
        var logger = new Mock<ILogger>();
        int count = 0;
        var error = await Assert.ThrowsExceptionAsync<JsonException>(async () =>
        {
            await foreach (var item in ReadAsync(stream, logger.Object))
                count++;
        });
        Assert.IsTrue(count > 0);
        Assert.IsTrue(error.Path!.EndsWith(".name", StringComparison.Ordinal));
        Assert.AreEqual(0L, error.LineNumber);
        Assert.AreEqual((long)json.Length - 2, error.BytePositionInLine);
        logger.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
            error, It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(20000)]
    public async Task ParseDataAsync_ReadFailure_IsNotSwallowed(int failAfter)
    {
        var bytes = Encoding.UTF8.GetBytes("{\"text\":\"" + new string('a', 50000) + "\"}");
        var failure = new IOException("Synthetic read failure");
        var stream = new TestStream(bytes, nonSeekable: false, failAfter: failAfter, failure: failure);
        var error = await Assert.ThrowsExceptionAsync<IOException>(async () => await ReadAsync(stream).ToListAsync());
        Assert.AreSame(failure, error);
        Assert.IsTrue(stream.Disposed);
    }

    [TestMethod]
    public async Task ParseDataAsync_CanceledRead_IsNotSwallowed()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var stream = new TestStream(Encoding.UTF8.GetBytes("{}"), nonSeekable: true);
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () =>
            await ReadAsync(stream, cancellationToken: cancellation.Token).ToListAsync());
        Assert.IsTrue(stream.Disposed);
    }

    [DataTestMethod]
    [DataRow("gz")]
    [DataRow("br")]
    [DataRow("zz")]
    public async Task ParseDataAsync_CompressedSingleObject_IsSupported(string compression)
    {
        using var data = new MemoryStream();
        using (Stream writer = compression switch
        {
            "gz" => new GZipStream(data, CompressionMode.Compress, leaveOpen: true),
            "br" => new BrotliStream(data, CompressionMode.Compress, leaveOpen: true),
            _ => new DeflateStream(data, CompressionMode.Compress, leaveOpen: true)
        })
        {
            writer.Write(Encoding.UTF8.GetBytes("{\"id\":1}"));
        }
        data.Position = 0;
        Stream stream = compression switch
        {
            "gz" => new GZipStream(data, CompressionMode.Decompress),
            "br" => new BrotliStream(data, CompressionMode.Decompress),
            _ => new DeflateStream(data, CompressionMode.Decompress)
        };
        var items = await ReadAsync(stream).ToListAsync();
        Assert.AreEqual(1, items.Count);
        Assert.AreEqual(1, items[0].GetValue("id"));
    }

    private static IAsyncEnumerable<IDataItem> ReadAsync(Stream stream, ILogger? logger = null, CancellationToken cancellationToken = default)
    {
        return new JsonFormatReader().ParseDataAsync(new StreamSource(stream), Config, logger ?? NullLogger.Instance, cancellationToken);
    }

    private sealed class StreamSource(Stream stream) : IComposableDataSource
    {
        public IEnumerable<IDataExtensionSettings> GetSettings() => Array.Empty<IDataExtensionSettings>();

        public async IAsyncEnumerable<Stream?> ReadSourceAsync(IConfiguration config, ILogger logger, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield return stream;
        }
    }

    private sealed class TestStream(byte[] bytes, bool nonSeekable, int maxReadSize = int.MaxValue,
        int failAfter = int.MaxValue, Exception? failure = null) : Stream
    {
        private readonly MemoryStream inner = new(bytes);
        public bool Disposed { get; private set; }
        public int BytesRead { get; private set; }
        public override bool CanSeek => !nonSeekable;
        public override bool CanRead => inner.CanRead;
        public override bool CanWrite => false;
        public override long Length => CanSeek ? inner.Length : throw new NotSupportedException();
        public override long Position
        {
            get => CanSeek ? inner.Position : throw new NotSupportedException();
            set => inner.Position = CanSeek ? value : throw new NotSupportedException();
        }
        public override long Seek(long offset, SeekOrigin origin) => CanSeek ? inner.Seek(offset, origin) : throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (BytesRead >= failAfter)
                throw failure ?? new IOException("Synthetic read failure");
            int read = inner.Read(buffer[..Math.Min(buffer.Length, maxReadSize)]);
            BytesRead += read;
            return read;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Read(buffer.Span));
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            if (disposing)
                inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
