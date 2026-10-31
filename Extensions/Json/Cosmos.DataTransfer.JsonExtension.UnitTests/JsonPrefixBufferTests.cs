using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Cosmos.DataTransfer.Common.UnitTests;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cosmos.DataTransfer.JsonExtension.UnitTests;

[TestClass]
public class JsonPrefixBufferTests
{
    private static readonly IConfiguration Config = TestHelpers.CreateConfig(new());

    [DataTestMethod]
    [DataRow(false, "[{\"id\":1}]", " ")]
    [DataRow(true, "[{\"id\":1}]", " ")]
    [DataRow(false, "{\"id\":1}", " ")]
    [DataRow(true, "{\"id\":1}", " ")]
    [DataRow(false, "", " ")]
    [DataRow(true, "", " ")]
    [DataRow(true, "[{\"id\":1}]", " \t\r\n")]
    public async Task ParseDataAsync_LargeWhitespacePrefix_HasBoundedAllocations(bool nonSeekable, string suffix, string pattern)
    {
        await ReadFirstAsync(new WhitespaceStream(256 * 1024, nonSeekable, suffix, whitespacePattern: pattern));
        var stream = new WhitespaceStream(16 * 1024 * 1024, nonSeekable, suffix, whitespacePattern: pattern);
        long before = GC.GetTotalAllocatedBytes(precise: true);
        await ReadFirstAsync(stream);
        long allocated = GC.GetTotalAllocatedBytes(precise: true) - before;
        Console.WriteLine($"Allocated {allocated} bytes for a 16 MiB prefix; non-seekable: {nonSeekable}, empty: {suffix.Length == 0}.");
        Assert.IsTrue(allocated < 256 * 1024, $"Allocated {allocated} bytes for lookahead and first item.");
        Assert.IsTrue(stream.Disposed);

        async Task ReadFirstAsync(Stream input)
        {
            await using var items = ReadAsync(input).GetAsyncEnumerator();
            if (suffix.Length == 0)
                await Assert.ThrowsExceptionAsync<JsonException>(async () => await items.MoveNextAsync());
            else
            {
                Assert.IsTrue(await items.MoveNextAsync());
                Assert.AreEqual(1, items.Current.GetValue("id"));
            }
        }
    }

    [DataTestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task ParseDataAsync_LongPrefixWithBom_PreservesErrorLocation(bool array, bool nonSeekable)
    {
        string json = new string(' ', 128 * 1024) + "\r\n\t" +
            (array ? "[" : "") + "{\"name\":}" + (array ? "]" : "");
        byte[] bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(json)).ToArray();
        // Match original-byte prefix replay so stock short-read path differences
        // do not get mistaken for a change caused by whitespace normalization.
        int bufferedPrefixLength = nonSeekable ? Array.IndexOf(bytes, (byte)(array ? '[' : '{')) + 1 : 0;
        await using var baseline = new ChunkedStream(bytes, nonSeekable: false, maxReadSize: 2,
            atomicBom: true, bufferedPrefixLength);
        JsonException expected = array
            ? await Assert.ThrowsExceptionAsync<JsonException>(async () =>
                await JsonSerializer.DeserializeAsyncEnumerable<Dictionary<string, object?>>(baseline).ToListAsync())
            : await Assert.ThrowsExceptionAsync<JsonException>(async () =>
                await JsonSerializer.DeserializeAsync<Dictionary<string, object?>>(baseline));
        Stream stream = new ChunkedStream(bytes, nonSeekable, maxReadSize: 2);
        var error = await Assert.ThrowsExceptionAsync<JsonException>(async () => await ReadAsync(stream).ToListAsync());
        Assert.AreEqual(expected.Path, error.Path);
        Assert.AreEqual(expected.LineNumber, error.LineNumber);
        Assert.AreEqual(expected.BytePositionInLine, error.BytePositionInLine);
        Assert.IsFalse(stream.CanRead);
    }

    [TestMethod]
    public async Task ReadFirstTokenAsync_SeekableSource_RewindsToOriginalPosition()
    {
        await using var source = new MemoryStream(Encoding.UTF8.GetBytes("ignored" + new string(' ', 128 * 1024) + "{}"));
        source.Position = 7;
        await using var input = new JsonFormatReader.PrefixReadStream(source);
        Assert.AreEqual((int)'{', await input.ReadFirstTokenAsync(CancellationToken.None));
        Assert.AreEqual(7L, source.Position);
        Assert.IsNotNull(await JsonSerializer.DeserializeAsync<Dictionary<string, object?>>(input));
    }

    [DataTestMethod]
    [DataRow("success")]
    [DataRow("parse-error")]
    [DataRow("whitespace-only")]
    [DataRow("early-exit")]
    [DataRow("read-failure")]
    [DataRow("cancellation")]
    [DataRow("consumer-failure")]
    public async Task ParseDataAsync_LongPrefix_DisposesSourceOnEveryExit(string scenario)
    {
        using var cancellation = new CancellationTokenSource();
        var failure = new IOException("Synthetic prefix read failure");
        string suffix = scenario switch
        {
            "parse-error" => "[{\"name\":}]",
            "whitespace-only" => "",
            _ => "[{\"id\":1}]"
        };
        var source = new WhitespaceStream(256 * 1024, nonSeekable: true, suffix,
            scenario == "read-failure" ? 128 * 1024 : long.MaxValue, failure,
            scenario == "cancellation" ? bytes =>
            {
                if (bytes >= 128 * 1024)
                    cancellation.Cancel();
            } : null);
        if (scenario == "read-failure")
        {
            var error = await Assert.ThrowsExceptionAsync<IOException>(async () =>
                await ReadAsync(source).ToListAsync());
            Assert.AreSame(failure, error);
        }
        else if (scenario == "cancellation")
        {
            try
            {
                await ReadAsync(source, cancellation.Token).ToListAsync();
                Assert.Fail("Prefix detection should have been canceled.");
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
        }
        else if (scenario is "parse-error" or "whitespace-only")
        {
            await Assert.ThrowsExceptionAsync<JsonException>(async () => await ReadAsync(source).ToListAsync());
        }
        else if (scenario == "consumer-failure")
        {
            var consumerFailure = new InvalidOperationException("Synthetic sink failure");
            var error = await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await foreach (var item in ReadAsync(source))
                    throw consumerFailure;
            });
            Assert.AreSame(consumerFailure, error);
        }
        else
        {
            await using var items = ReadAsync(source).GetAsyncEnumerator();
            Assert.IsTrue(await items.MoveNextAsync());
            if (scenario == "success")
                Assert.IsFalse(await items.MoveNextAsync());
        }
        Assert.IsTrue(source.Disposed);
    }

    public static IEnumerable<object[]> ParserCases
    {
        get
        {
            var prefixes = new[]
            {
                "", " \t\r", "\n", "\r\n \t\r \n\t  ", new string('\r', 4097),
                new string(' ', 4092) + "\r\n\t ",
                string.Concat(Enumerable.Repeat(" \t\r\n", 3000)),
                "\n" + new string('\t', 20000) + "\r",
                new string(' ', 16400) + "\n" + new string('\t', 16400) + "\r\n "
            };
            const string item = "{\"id\":7,\"text\":\"\u00e9\u20ac\ud83d\ude00\",\"flag\":true,\"optional\":null}";
            var suffixes = new[]
            {
                (Name: "object", Json: item, SingleObject: true),
                (Name: "array", Json: "[null," + item + "]", SingleObject: false),
                (Name: "empty-array", Json: "[]", SingleObject: false),
                (Name: "null", Json: "null", SingleObject: false),
                (Name: "object-error", Json: "{\"a\":{\"name\":}}", SingleObject: true),
                (Name: "array-error", Json: "[{\"a\":{\"name\":}}]", SingleObject: false),
                (Name: "object-trailing", Json: "{} {}", SingleObject: true),
                (Name: "array-trailing", Json: "[] []", SingleObject: false),
                (Name: "whitespace-only", Json: "", SingleObject: false)
            };
            for (int i = 0; i < prefixes.Length; i++)
            foreach (var suffix in suffixes)
            foreach (bool bom in new[] { false, true })
            foreach (bool nonSeekable in new[] { false, true })
            foreach (int chunk in new[] { 1, 4096 })
            {
                byte[] bytes = Encoding.UTF8.GetBytes(prefixes[i] + suffix.Json);
                if (bom)
                    bytes = Encoding.UTF8.GetPreamble().Concat(bytes).ToArray();
                yield return [$"prefix-{i}-{suffix.Name}-bom-{bom}", bytes, suffix.SingleObject, nonSeekable, chunk];
            }

            byte[][] invalidPrefixes =
            [
                [0xEF], [0xEF, 0xBB], [0xEF, 0x00, 0xBF], [0x00], [0x0B], [0xFF],
                [0xC2, 0xA0], [0xEF, 0xBB, 0xBF, 0xEF, 0xBB, 0xBF, (byte)'{', (byte)'}']
            ];
            for (int i = 0; i < invalidPrefixes.Length; i++)
            foreach (bool nonSeekable in new[] { false, true })
                yield return [$"invalid-prefix-{i}", invalidPrefixes[i], false, nonSeekable, 1];
        }
    }

    [DataTestMethod]
    [DynamicData(nameof(ParserCases), DynamicDataDisplayName = nameof(ParserCaseDisplayName))]
    public async Task ParseDataAsync_WhitespaceReplay_MatchesStockParser(string name, byte[] bytes,
        bool singleObject, bool nonSeekable, int maxReadSize)
    {
        await using var baseline = new ChunkedStream(bytes, nonSeekable: false, maxReadSize, atomicBom: true);
        JsonException? expectedError = null;
        var expected = new List<Dictionary<string, object?>>();
        try
        {
            if (singleObject)
            {
                var item = await JsonSerializer.DeserializeAsync<Dictionary<string, object?>>(baseline);
                if (item != null)
                    expected.Add(item);
            }
            else
            {
                await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<Dictionary<string, object?>>(baseline))
                {
                    if (item != null)
                        expected.Add(item);
                }
            }
        }
        catch (JsonException ex)
        {
            expectedError = ex;
        }

        var stream = new ChunkedStream(bytes, nonSeekable, maxReadSize);
        if (expectedError != null)
        {
            var error = await Assert.ThrowsExceptionAsync<JsonException>(async () => await ReadAsync(stream).ToListAsync());
            AssertSameError(expectedError, error, name);
        }
        else
        {
            var items = await ReadAsync(stream).ToListAsync();
            var actual = items.Select(item => item.GetFieldNames().ToDictionary(field => field, item.GetValue)).ToList();
            Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual), name);
        }
        Assert.IsTrue(stream.Disposed, name);
    }

    public static string ParserCaseDisplayName(MethodInfo method, object[] data) =>
        $"{method.Name} ({data[0]}, non-seekable: {data[3]}, chunk: {data[4]})";

    [DataTestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task ParseDataAsync_LateArrayError_PreservesStockParserCoordinates(bool bom, bool nonSeekable)
    {
        string prefix = string.Concat(Enumerable.Repeat(" \t\r\n", 20000)) + new string(' ', 17);
        string json = prefix + "[" + string.Join(",", Enumerable.Repeat("{\"id\":1}", 10000)) + ",{\"name\":}]";
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        if (bom)
            bytes = Encoding.UTF8.GetPreamble().Concat(bytes).ToArray();
        await using var baseline = new MemoryStream(bytes);
        var expected = await Assert.ThrowsExceptionAsync<JsonException>(async () =>
            await JsonSerializer.DeserializeAsyncEnumerable<Dictionary<string, object?>>(baseline).ToListAsync());
        var stream = new ChunkedStream(bytes, nonSeekable, maxReadSize: 4096);
        var error = await Assert.ThrowsExceptionAsync<JsonException>(async () => await ReadAsync(stream).ToListAsync());
        // The stock streaming reader can report batch-relative array indices.
        Assert.AreEqual(expected.LineNumber, error.LineNumber);
        Assert.AreEqual(expected.BytePositionInLine, error.BytePositionInLine);
        StringAssert.EndsWith(error.Path!, ".name");
        Assert.IsTrue(stream.Disposed);
    }

    [DataTestMethod]
    [DataRow("memory", 1, false)]
    [DataRow("array", 2, false)]
    [DataRow("span", 7, false)]
    [DataRow("sync", 4096, false)]
    [DataRow("byte", 1, false)]
    [DataRow("memory", 1, true)]
    [DataRow("array", 2, true)]
    [DataRow("span", 7, true)]
    [DataRow("sync", 4096, true)]
    [DataRow("byte", 1, true)]
    public async Task ReadFirstTokenAsync_Replay_PreservesLengthBomAndDocumentBytes(string readMethod, int readSize, bool nonSeekable)
    {
        byte[] prefix = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(" \r\n\t \r\n \t")).ToArray();
        byte[] document = Encoding.UTF8.GetBytes("{\"text\":\"\u00e9\u20ac" + new string('x', 10000) + "\"}\r\n");
        byte[] bytes = prefix.Concat(document).ToArray();
        await using var source = new ChunkedStream(bytes, nonSeekable, maxReadSize: 2);
        await using var input = new JsonFormatReader.PrefixReadStream(source);
        Assert.AreEqual((int)'{', await input.ReadFirstTokenAsync(CancellationToken.None));
        Assert.AreEqual(0, await input.ReadAsync(Memory<byte>.Empty));
        using var replayed = new MemoryStream();
        var buffer = new byte[readSize];
        while (true)
        {
            int count = readMethod switch
            {
                "memory" => await input.ReadAsync(buffer.AsMemory()),
                "array" => await input.ReadAsync(buffer, 0, buffer.Length),
                "span" => input.Read(buffer.AsSpan()),
                "byte" => ReadByte(input, buffer),
                _ => input.Read(buffer, 0, buffer.Length)
            };
            if (count == 0)
                break;
            replayed.Write(buffer, 0, count);
        }
        byte[] actual = replayed.ToArray();
        Assert.AreEqual(bytes.Length, actual.Length);
        CollectionAssert.AreEqual(Encoding.UTF8.GetPreamble(), actual[..3]);
        CollectionAssert.AreEqual(document, actual[prefix.Length..]);

        static int ReadByte(Stream input, byte[] buffer)
        {
            int value = input.ReadByte();
            if (value == -1)
                return 0;
            buffer[0] = (byte)value;
            return 1;
        }
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReadFirstTokenAsync_CanceledReplay_DoesNotConsumeBufferedData(bool legacyRead)
    {
        await using var source = new WhitespaceStream(128 * 1024, nonSeekable: true);
        await using var input = new JsonFormatReader.PrefixReadStream(source);
        await input.ReadFirstTokenAsync(CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () =>
        {
            var buffer = new byte[1];
            if (legacyRead)
                await input.ReadAsync(buffer, 0, buffer.Length, cancellation.Token);
            else
                await input.ReadAsync(buffer.AsMemory(), cancellation.Token);
        });
        var items = await JsonSerializer.DeserializeAsyncEnumerable<Dictionary<string, object?>>(input).ToListAsync();
        Assert.AreEqual(1, items.Count);
    }

    [TestMethod]
    public async Task PrefixReadStream_NullByteArrayBuffer_ThrowsEvenForZeroByteReads()
    {
        using var source = new MemoryStream();
        using var input = new JsonFormatReader.PrefixReadStream(source);
        Assert.ThrowsException<ArgumentNullException>(() => input.Read(null!, 0, 0));
        await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () =>
            await input.ReadAsync(null!, 0, 0));
    }

    [TestMethod]
    public async Task PrefixReadStream_Disposal_ClosesViewButLeavesSourceToItsOwner()
    {
        await using var source = new WhitespaceStream(128 * 1024, nonSeekable: true);
        var input = new JsonFormatReader.PrefixReadStream(source);
        await input.ReadFirstTokenAsync(CancellationToken.None);
        await input.DisposeAsync();
        Assert.IsFalse(input.CanRead);
        Assert.IsTrue(source.CanRead);
        Assert.ThrowsException<ObjectDisposedException>(() => input.ReadByte());
        await Assert.ThrowsExceptionAsync<ObjectDisposedException>(async () => await input.ReadAsync(new byte[1]));
    }

    private static void AssertSameError(JsonException expected, JsonException actual, string name)
    {
        Assert.AreEqual(expected.Path, actual.Path, name);
        Assert.AreEqual(expected.LineNumber, actual.LineNumber, name);
        Assert.AreEqual(expected.BytePositionInLine, actual.BytePositionInLine, name);
        Assert.AreEqual(expected.Message, actual.Message, name);
    }

    private static IAsyncEnumerable<IDataItem> ReadAsync(Stream stream, CancellationToken cancellationToken = default) =>
        new JsonFormatReader().ParseDataAsync(new StreamSource(stream), Config, NullLogger.Instance, cancellationToken);

    private sealed class StreamSource(Stream stream) : IComposableDataSource
    {
        public IEnumerable<IDataExtensionSettings> GetSettings() => [];

        public async IAsyncEnumerable<Stream?> ReadSourceAsync(IConfiguration config, ILogger logger,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield return stream;
        }
    }

    private sealed class ChunkedStream(byte[] bytes, bool nonSeekable, int maxReadSize, bool atomicBom = false,
        int bufferedPrefixLength = 0) : Stream
    {
        private readonly MemoryStream inner = new(bytes);
        public bool Disposed { get; private set; }
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => !nonSeekable && inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => CanSeek ? inner.Length : throw new NotSupportedException();
        public override long Position
        {
            get => CanSeek ? inner.Position : throw new NotSupportedException();
            set => inner.Position = CanSeek ? value : throw new NotSupportedException();
        }
        public override long Seek(long offset, SeekOrigin origin) => CanSeek ? inner.Seek(offset, origin) : throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            int count = inner.Position < bufferedPrefixLength
                ? (int)Math.Min(buffer.Length, bufferedPrefixLength - inner.Position)
                : Math.Min(buffer.Length, maxReadSize);
            return inner.Read(buffer[..count]);
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (atomicBom && bufferedPrefixLength == 0 && inner.Position == 0 && buffer.Length >= 3 &&
                bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return ValueTask.FromResult(this.ReadAtLeast(buffer.Span, 3, throwOnEndOfStream: false));
            }
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

    private sealed class WhitespaceStream(long whitespace, bool nonSeekable, string suffix = "[{\"id\":1}]",
        long failAfter = long.MaxValue, Exception? failure = null, Action<long>? afterRead = null,
        string whitespacePattern = " ") : Stream
    {
        private readonly byte[] tail = Encoding.UTF8.GetBytes(suffix);
        private readonly byte[] pattern = Encoding.UTF8.GetBytes(whitespacePattern);
        private long position;
        public bool Disposed { get; private set; }
        public override bool CanRead => !Disposed;
        public override bool CanSeek => !nonSeekable;
        public override bool CanWrite => false;
        public override long Length => CanSeek ? whitespace + tail.Length : throw new NotSupportedException();
        public override long Position
        {
            get => CanSeek ? position : throw new NotSupportedException();
            set => position = CanSeek ? value : throw new NotSupportedException();
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (position >= failAfter)
                throw failure ?? new IOException("Synthetic prefix read failure");
            int count;
            if (position < whitespace)
            {
                count = (int)Math.Min(buffer.Length, whitespace - position);
                if (pattern.Length == 1)
                    buffer[..count].Fill(pattern[0]);
                else
                {
                    for (int i = 0; i < count; i++)
                        buffer[i] = pattern[(int)((position + i) % pattern.Length)];
                }
            }
            else
            {
                int offset = (int)(position - whitespace);
                count = Math.Min(buffer.Length, tail.Length - offset);
                tail.AsSpan(offset, count).CopyTo(buffer);
            }
            position += count;
            afterRead?.Invoke(position);
            return count;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Read(buffer.Span));
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
