namespace Cosmos.DataTransfer.Interfaces;

/// <summary>
/// Represents progress information for data transfer operations.
/// </summary>
public class DataTransferProgress
{
    /// <summary>
    /// Gets or sets the current number of items processed.
    /// </summary>
    public int ItemCount { get; set; }

    /// <summary>
    /// Gets or sets the current number of bytes transferred.
    /// </summary>
    public long BytesTransferred { get; set; }

    /// <summary>
    /// Gets or sets a descriptive message about the current operation.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets additional context information for the progress.
    /// </summary>
    public Dictionary<string, object>? Context { get; set; }

    public DataTransferProgress()
    {
    }

    public DataTransferProgress(int itemCount, long bytesTransferred = 0, string? message = null)
    {
        ItemCount = itemCount;
        BytesTransferred = bytesTransferred;
        Message = message;
    }
}