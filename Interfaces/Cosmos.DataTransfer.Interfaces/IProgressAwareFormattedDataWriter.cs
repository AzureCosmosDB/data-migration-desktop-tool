using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.Interfaces;

/// <summary>
/// Extended interface for formatted data writers that support progress reporting.
/// </summary>
public interface IProgressAwareFormattedDataWriter : IFormattedDataWriter
{
    /// <summary>
    /// Formats data asynchronously with progress reporting support.
    /// </summary>
    /// <param name="dataItems">The data items to format</param>
    /// <param name="target">The target stream to write formatted data to</param>
    /// <param name="config">Configuration settings</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="progress">Progress reporter for tracking item processing</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the async operation</returns>
    Task FormatDataAsync(IAsyncEnumerable<IDataItem> dataItems, Stream target, IConfiguration config, ILogger logger, IProgress<DataTransferProgress>? progress, CancellationToken cancellationToken = default);
}