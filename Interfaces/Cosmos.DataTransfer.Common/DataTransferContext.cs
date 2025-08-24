using Cosmos.DataTransfer.Interfaces;

namespace Cosmos.DataTransfer.Common;

/// <summary>
/// Shared context for passing information between formatters and sinks during data transfer operations.
/// </summary>
public class DataTransferContext
{
    private DataTransferProgress _currentProgress = new();
    private readonly object _lockObject = new();

    /// <summary>
    /// Gets the current progress state (thread-safe).
    /// </summary>
    public DataTransferProgress GetCurrentProgress()
    {
        lock (_lockObject)
        {
            return new DataTransferProgress(_currentProgress.ItemCount, _currentProgress.BytesTransferred, _currentProgress.Message)
            {
                Context = _currentProgress.Context?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };
        }
    }

    /// <summary>
    /// Updates the current progress state (thread-safe).
    /// </summary>
    public void UpdateProgress(DataTransferProgress progress)
    {
        lock (_lockObject)
        {
            _currentProgress.ItemCount = progress.ItemCount;
            _currentProgress.BytesTransferred = progress.BytesTransferred;
            _currentProgress.Message = progress.Message;
            _currentProgress.Context = progress.Context;
        }
    }

    /// <summary>
    /// Increments the item count atomically.
    /// </summary>
    public void IncrementItemCount()
    {
        lock (_lockObject)
        {
            _currentProgress.ItemCount++;
        }
    }
}