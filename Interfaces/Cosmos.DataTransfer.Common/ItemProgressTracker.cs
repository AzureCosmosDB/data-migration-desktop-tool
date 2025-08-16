using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.Common;

/// <summary>
/// Helper class for tracking and logging item processing progress during data migrations.
/// </summary>
public class ItemProgressTracker
{
    private readonly ILogger _logger;
    private readonly int _progressFrequency;
    private int _itemCount;

    /// <summary>
    /// Initializes a new instance of the ItemProgressTracker class.
    /// </summary>
    /// <param name="logger">Logger instance for writing progress messages</param>
    /// <param name="progressFrequency">Frequency at which to log progress updates (default: 1000)</param>
    public ItemProgressTracker(ILogger logger, int progressFrequency = 1000)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _progressFrequency = progressFrequency;
        _itemCount = 0;
    }

    /// <summary>
    /// Gets the current item count.
    /// </summary>
    public int ItemCount => _itemCount;

    /// <summary>
    /// Increments the item count and logs progress if threshold is reached.
    /// </summary>
    public void IncrementItem()
    {
        _itemCount++;
        
        if (_itemCount % _progressFrequency == 0)
        {
            _logger.LogInformation("Processed {ItemCount} items for transfer to Azure Blob", _itemCount);
        }
    }

    /// <summary>
    /// Logs the final item count summary.
    /// </summary>
    public void LogFinalCount()
    {
        if (_itemCount > 0)
            _logger.LogInformation("Completed processing {ItemCount} total items for transfer to Azure Blob", _itemCount);
        else
            _logger.LogWarning("No items were processed for transfer to Azure Blob");
    }
}