using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.Common;

/// <summary>
/// Helper class for tracking and logging document processing progress during data migrations.
/// </summary>
public class DocumentProgressTracker
{
    private readonly ILogger _logger;
    private readonly int _progressFrequency;
    private int _documentCount;

    /// <summary>
    /// Initializes a new instance of the DocumentProgressTracker class.
    /// </summary>
    /// <param name="logger">Logger instance for writing progress messages</param>
    /// <param name="progressFrequency">Frequency at which to log progress updates (default: 1000)</param>
    public DocumentProgressTracker(ILogger logger, int progressFrequency = 1000)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _progressFrequency = progressFrequency;
        _documentCount = 0;
    }

    /// <summary>
    /// Gets the current document count.
    /// </summary>
    public int DocumentCount => _documentCount;

    /// <summary>
    /// Increments the document count and logs progress if threshold is reached.
    /// </summary>
    public void IncrementDocument()
    {
        _documentCount++;
        
        if (_documentCount % _progressFrequency == 0)
        {
            _logger.LogInformation("Processed {DocumentCount} documents for transfer to Azure Blob", _documentCount);
        }
    }

    /// <summary>
    /// Logs the final document count summary.
    /// </summary>
    public void LogFinalCount()
    {
        if (_documentCount > 0)
            _logger.LogInformation("Completed processing {DocumentCount} total documents for transfer to Azure Blob", _documentCount);
        else
            _logger.LogWarning("No documents were processed for transfer to Azure Blob");
    }
}