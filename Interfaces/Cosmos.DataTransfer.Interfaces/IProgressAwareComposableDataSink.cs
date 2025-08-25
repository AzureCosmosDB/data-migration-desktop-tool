using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.Interfaces;

/// <summary>
/// Extended interface for composable data sinks that support progress reporting.
/// </summary>
public interface IProgressAwareComposableDataSink : IComposableDataSink
{
    /// <summary>
    /// Writes data to the target asynchronously with progress reporting support.
    /// </summary>
    /// <param name="writeToStream">Function to write data to a stream</param>
    /// <param name="config">Configuration settings</param>
    /// <param name="dataSource">The data source extension</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="progress">Progress reporter for tracking transfer progress</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the async operation</returns>
    Task WriteToTargetAsync(Func<Stream, Task> writeToStream, IConfiguration config, IDataSourceExtension dataSource, ILogger logger, IProgress<DataTransferProgress>? progress, CancellationToken cancellationToken = default);
}