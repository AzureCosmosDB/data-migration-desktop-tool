using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.Common;

/// <summary>
/// Progress reporter for data transfer operations that handles logging.
/// </summary>
public class DataTransferProgressReporter : IProgress<DataTransferProgress>
{
    private readonly ILogger _logger;
    private readonly int _progressFrequency;
    private readonly string _operationType;
    private readonly DataTransferContext? _context;

    /// <summary>
    /// Gets the current data transfer context.
    /// </summary>
    public DataTransferContext? Context => _context;

    public DataTransferProgressReporter(ILogger logger, int progressFrequency = 1000, string operationType = "item", DataTransferContext? context = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _progressFrequency = progressFrequency;
        _operationType = operationType;
        _context = context;
    }

    public void Report(DataTransferProgress value)
    {
        if (value == null) return;

        // Update shared context if available
        _context?.UpdateProgress(value);

        // Log progress at specified frequency
        if (value.ItemCount > 0 && value.ItemCount % _progressFrequency == 0)
        {
            if (!string.IsNullOrEmpty(value.Message))
            {
                _logger.LogInformation(value.Message);
            }
            else
            {
                _logger.LogInformation("Formatted {ItemCount} {OperationType}s for transfer", value.ItemCount, _operationType);
            }
        }

        // Log final summary if provided
        if (!string.IsNullOrEmpty(value.Message) && value.ItemCount > 0 && value.ItemCount % _progressFrequency != 0)
        {
            _logger.LogInformation(value.Message);
        }
    }
}