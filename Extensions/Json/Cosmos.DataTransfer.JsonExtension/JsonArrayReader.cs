using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Cosmos.DataTransfer.JsonExtension;

internal static class JsonArrayReader
{
    public static async IAsyncEnumerable<Dictionary<string, object?>?> ReadAsync(
        Stream stream, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var items = JsonSerializer
            .DeserializeAsyncEnumerable<Dictionary<string, object?>>(stream, cancellationToken: cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        long itemsRead = 0;

        while (true)
        {
            bool hasItem;
            try
            {
                hasItem = await items.MoveNextAsync();
            }
            catch (JsonException ex) when (TryGetGlobalPath(ex.Path, itemsRead, out var path))
            {
                throw new JsonException(
                    FormattableString.Invariant(
                        $"Failed to parse JSON array. Path: {path} | LineNumber: {ex.LineNumber} | BytePositionInLine: {ex.BytePositionInLine}."),
                    path, ex.LineNumber, ex.BytePositionInLine, ex);
            }

            if (!hasItem)
                yield break;

            // Count null entries too: the formatter filters them, but they occupy array indices.
            itemsRead++;
            yield return items.Current;
        }
    }

    private static bool TryGetGlobalPath(string? path, long itemsRead, out string? globalPath)
    {
        globalPath = null;
        if (itemsRead == 0 || path == null || !path.StartsWith("$[", StringComparison.Ordinal))
            return false;

        int end = path.IndexOf(']', 2);
        if (end < 0 || !long.TryParse(path.AsSpan(2, end - 2), NumberStyles.None, CultureInfo.InvariantCulture, out long batchIndex))
            return false;

        // System.Text.Json 8 drains its queue after each successful streaming read, but
        // ReadStack.JsonPath uses that queue's Count. Only the outer index is batch-relative.
        // Recheck this invariant when upgrading System.Text.Json; never rewrite Message.
        globalPath = "$[" + (itemsRead + batchIndex).ToString(CultureInfo.InvariantCulture) + path[end..];
        return true;
    }
}
