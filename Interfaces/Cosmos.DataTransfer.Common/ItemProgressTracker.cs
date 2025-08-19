using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.Common;

/// <summary>
/// Helper class for tracking and logging item processing progress during data migrations.
/// </summary>
public class ItemProgressTracker
{
    [ThreadStatic]
    private static int _currentItemCount;
    
    private readonly ILogger _logger;
    private readonly int _progressFrequency;
    private readonly string? _blobName;
    private readonly string? _containerName;
    private int _itemCount;

    /// <summary>
    /// Initializes a new instance of the ItemProgressTracker class.
    /// </summary>
    /// <param name="logger">Logger instance for writing progress messages</param>
    /// <param name="progressFrequency">Frequency at which to log progress updates (default: 1000)</param>
    /// <param name="blobName">Optional blob name for more detailed final summary</param>
    /// <param name="containerName">Optional container name for more detailed final summary</param>
    public ItemProgressTracker(ILogger logger, int progressFrequency = 1000, string? blobName = null, string? containerName = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _progressFrequency = progressFrequency;
        _blobName = blobName;
        _containerName = containerName;
        _itemCount = 0;
    }

    /// <summary>
    /// Gets the current item count.
    /// </summary>
    public int ItemCount => _itemCount;

    /// <summary>
    /// Gets the current item count from the thread-static context (for sinks to access).
    /// </summary>
    public static int GetCurrentItemCount() => _currentItemCount;

    /// <summary>
    /// Increments the item count and logs progress if threshold is reached.
    /// </summary>
    public void IncrementItem()
    {
        _itemCount++;
        _currentItemCount = _itemCount;
        
        if (_itemCount % _progressFrequency == 0)
        {
            _logger.LogInformation("Formatted {ItemCount} items for transfer to Azure Blob", _itemCount);
        }
    }

    /// <summary>
    /// Completes the item counting and makes the final count available to sinks.
    /// The actual final logging will be done by the sink with comprehensive details.
    /// </summary>
    public void CompleteFormatting()
    {
        _currentItemCount = _itemCount;
        
        // Only log if no items were processed (warning case)
        if (_itemCount == 0)
        {
            _logger.LogWarning("No items were formatted for transfer to Azure Blob");
        }
    }
}