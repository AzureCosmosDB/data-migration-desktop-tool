using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.Common;

/// <summary>
/// Static utility class for tracking and logging item processing progress during data migrations.
/// Since only one migration runs at a time, this uses static thread safe class.
/// It is designed to be initialized at the start of a migration and reset for each new migration
public static class ItemProgressTracker
{
    private static int _itemCount;
    private static ILogger? _logger;
    private static int _progressFrequency = 1000;

    /// <summary>
    /// Gets the current item count.
    /// </summary>
    public static int ItemCount => _itemCount;

    /// <summary>
    /// Initializes the tracker for a new migration.
    /// </summary>
    /// <param name="logger">Logger instance for writing progress messages</param>
    /// <param name="progressFrequency">Frequency at which to log progress updates (default: 1000)</param>
    public static void Initialize(ILogger logger, int progressFrequency = 1000)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _progressFrequency = progressFrequency;
        _itemCount = 0;
    }

    /// <summary>
    /// Resets the tracker state. Should be called at the beginning of each new migration.
    /// </summary>
    public static void Reset()
    {
        _itemCount = 0;
        _logger = null;
        _progressFrequency = 1000;
    }

    /// <summary>
    /// Increments the item count and logs progress if threshold is reached.
    /// </summary>
    public static void IncrementItem()
    {
        int currentCount = Interlocked.Increment(ref _itemCount);

        if (_logger != null && currentCount % _progressFrequency == 0)
        {
            _logger.LogInformation("Processed {ItemCount} items for data migration.", currentCount);
        }
    }

    /// <summary>
    /// Completes the item counting.
    /// The actual final logging will be done by the sink with comprehensive details.
    /// </summary>
    public static void CompleteFormatting()
    {
        // Only log if no items were processed (warning case)
        if (_logger != null && _itemCount == 0)
        {
            _logger.LogWarning("No items were process for data migration.");
        }
    }
}