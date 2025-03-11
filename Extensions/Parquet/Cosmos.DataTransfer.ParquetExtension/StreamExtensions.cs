namespace Cosmos.DataTransfer.ParquetExtension;

public static class StreamExtensions
{
    public static bool HasSize(this Stream s)
    {
        try
        {
            var size = s.Length;
            return true;
        }
        catch (NotSupportedException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        return false;
    }
}