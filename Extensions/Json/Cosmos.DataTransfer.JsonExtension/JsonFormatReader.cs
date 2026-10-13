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
        using var prefix = new MemoryStream();
        var firstToken = await ReadPrefixAsync(jsonStream, prefix, cancellationToken);
        prefix.Position = 0;
        using var input = new PrefixReadStream(prefix, jsonStream);
        if (firstToken == '{')
        {
            yield return await JsonSerializer.DeserializeAsync<Dictionary<string, object?>>(input, cancellationToken: cancellationToken);
        }
        else
        {
            await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<Dictionary<string, object?>>(input, cancellationToken: cancellationToken))
            {
                yield return item;
            }
        }
    }

    private static async Task<int> ReadPrefixAsync(Stream stream, MemoryStream prefix, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        int count = await stream.ReadAtLeastAsync(buffer.AsMemory(0, 3), 3, throwOnEndOfStream: false, cancellationToken);
        int start = count == 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF ? 3 : 0;
        while (count > 0)
        {
            prefix.Write(buffer, 0, count);
            for (int i = start; i < count; i++)
            {
                if (buffer[i] is not ((byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n'))
                    return buffer[i];
            }

            start = 0;
            count = await stream.ReadAsync(buffer, cancellationToken);
        }
        return -1;
    }

    // Replay only the sniffed prefix, including the BOM and whitespace, to preserve
    // the serializer's original error positions without seeking or buffering arrays.
    private sealed class PrefixReadStream(Stream prefix, Stream source) : Stream
    {
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
            int read = prefix.Read(buffer, offset, count);
            return read > 0 ? read : source.Read(buffer, offset, count);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int read = prefix.Read(buffer.Span);
            return read > 0 ? ValueTask.FromResult(read) : source.ReadAsync(buffer, cancellationToken);
        }
    }

    public IEnumerable<IDataExtensionSettings> GetSettings()
    {
        yield break;
    }
}