using System.Runtime.CompilerServices;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Cosmos.DataTransfer.JsonExtension;

public class JsonFormatReader : IFormattedDataReader
{
    public async IAsyncEnumerable<IDataItem> ParseDataAsync(IComposableDataSource sourceExtension, IConfiguration config, ILogger logger, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var streams = sourceExtension.ReadSourceAsync(config, logger, cancellationToken);
        int streamNumber = 0;

        await foreach (var stream in streams.WithCancellation(cancellationToken))
        {
            streamNumber++;
            if (stream != null)
            {
                await using var ownedStream = stream;
                await using var items = ReadJsonItemsAsync(stream, cancellationToken).GetAsyncEnumerator(cancellationToken);
                while (true)
                {
                    bool hasItem;
                    try
                    {
                        hasItem = await items.MoveNextAsync();
                    }
                    catch (JsonException ex)
                    {
                        logger.LogError(ex,
                            "Failed to parse JSON from {SourceType}, stream {StreamNumber}. See the preceding source read log for its location.",
                            sourceExtension.GetType().Name, streamNumber);
                        throw;
                    }

                    if (!hasItem)
                        break;
                    if (items.Current != null)
                        yield return new JsonDictionaryDataItem(items.Current);
                }
            }
        }
    }

    private static async IAsyncEnumerable<Dictionary<string, object?>?> ReadJsonItemsAsync(Stream jsonStream, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var input = new PrefixReadStream(jsonStream);
        var firstToken = await input.ReadFirstTokenAsync(cancellationToken);
        if (firstToken == '{')
        {
            yield return await JsonSerializer.DeserializeAsync<Dictionary<string, object?>>(input, cancellationToken: cancellationToken);
        }
        else
        {
            await foreach (var item in JsonArrayReader.ReadAsync(input, cancellationToken))
            {
                yield return item;
            }
        }
    }

    internal sealed class PrefixReadStream(Stream source) : Stream
    {
        private readonly bool rewindSource = source.CanSeek;
        private byte[]? prefix;
        private int prefixOffset;
        private int prefixLength;
        private int bomRemaining;
        private long spacesBeforeLineFeedsRemaining;
        private long lineFeedsRemaining;
        private long columnRemaining;
        private bool readSourceBom;
        private bool disposed;

        private static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

        internal async Task<int> ReadFirstTokenAsync(CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            long originalPosition = rewindSource ? source.Position : 0;
            var buffer = new byte[4096];
            int count = await source.ReadAtLeastAsync(buffer.AsMemory(0, 3), 3, throwOnEndOfStream: false, cancellationToken);
            bool hasBom = count == 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF;
            int start = hasBom ? 3 : 0;
            int firstToken = -1;
            int tokenOffset = 0;
            long whitespaceLength = 0;
            long lineFeeds = 0;
            long column = 0;
            while (count > 0 && firstToken == -1)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int i = start; i < count; i++)
                {
                    byte value = buffer[i];
                    if (value is not ((byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n'))
                    {
                        firstToken = value;
                        tokenOffset = i;
                        break;
                    }

                    whitespaceLength++;
                    if (value == '\n')
                    {
                        lineFeeds++;
                        column = 0;
                    }
                    else
                        column++;
                }

                if (firstToken == -1)
                {
                    start = 0;
                    count = await source.ReadAsync(buffer, cancellationToken);
                }
            }
            if (rewindSource)
            {
                source.Position = originalPosition;
                readSourceBom = hasBom;
            }
            else
            {
                bomRemaining = hasBom ? 3 : 0;
                spacesBeforeLineFeedsRemaining = whitespaceLength - lineFeeds - column;
                lineFeedsRemaining = lineFeeds;
                columnRemaining = column;
                if (firstToken != -1)
                {
                    prefix = buffer;
                    prefixOffset = tokenOffset;
                    prefixLength = count - tokenOffset;
                }
            }
            return firstToken;
        }

        public override bool CanRead => !disposed && source.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private int ReadPrefix(Span<byte> buffer)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            int written = 0;
            int count = Math.Min(buffer.Length, bomRemaining);
            Utf8Bom.Slice(3 - bomRemaining, count).CopyTo(buffer);
            bomRemaining -= count;
            written += count;
            buffer = buffer[count..];

            // Preserve byte offsets, LF count and final byte column without
            // retaining the original whitespace.
            count = (int)Math.Min(buffer.Length, spacesBeforeLineFeedsRemaining);
            buffer[..count].Fill((byte)' ');
            spacesBeforeLineFeedsRemaining -= count;
            written += count;
            buffer = buffer[count..];

            count = (int)Math.Min(buffer.Length, lineFeedsRemaining);
            buffer[..count].Fill((byte)'\n');
            lineFeedsRemaining -= count;
            written += count;
            buffer = buffer[count..];

            count = (int)Math.Min(buffer.Length, columnRemaining);
            buffer[..count].Fill((byte)' ');
            columnRemaining -= count;
            written += count;
            buffer = buffer[count..];

            if (prefix != null)
            {
                count = Math.Min(buffer.Length, prefixLength);
                prefix.AsSpan(prefixOffset, count).CopyTo(buffer);
                prefixOffset += count;
                prefixLength -= count;
                written += count;
            }
            return written;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);
            return Read(buffer.AsSpan(offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            int read = ReadPrefix(buffer);
            if (readSourceBom && buffer.Length > 0)
            {
                readSourceBom = false;
                return source.ReadAtLeast(buffer, Math.Min(3, buffer.Length), throwOnEndOfStream: false);
            }
            return read > 0 ? read : source.Read(buffer);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBufferArguments(buffer, offset, count);
            return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int read = ReadPrefix(buffer.Span);
            // The streaming reader expects a BOM in its first read.
            if (readSourceBom && buffer.Length > 0)
            {
                readSourceBom = false;
                return source.ReadAtLeastAsync(buffer, Math.Min(3, buffer.Length), throwOnEndOfStream: false, cancellationToken);
            }
            return read > 0 ? ValueTask.FromResult(read) : source.ReadAsync(buffer, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                disposed = true;
            base.Dispose(disposing);
        }
    }

    public IEnumerable<IDataExtensionSettings> GetSettings()
    {
        yield break;
    }
}