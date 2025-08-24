using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.Common;

public abstract class CompositeSinkExtension<TSink, TFormatter> : IDataSinkExtensionWithSettings
    where TSink : class, IComposableDataSink, new()
    where TFormatter : class, IFormattedDataWriter, new()
{
    public abstract string DisplayName { get; }

    public async Task WriteAsync(IAsyncEnumerable<IDataItem> dataItems, IConfiguration config, IDataSourceExtension dataSource, ILogger logger, CancellationToken cancellationToken = default)
    {
        var sink = new TSink();
        var formatter = new TFormatter();

        // Create shared context for passing data between formatter and sink
        var context = new DataTransferContext();
        var progressReporter = new DataTransferProgressReporter(logger, 1000, "item", context);

        async Task WriteToStream(Stream stream)
        {
            // Check if formatter supports progress reporting
            if (formatter is IProgressAwareFormattedDataWriter progressAwareFormatter)
            {
                await progressAwareFormatter.FormatDataAsync(dataItems, stream, config, logger, progressReporter, cancellationToken);
            }
            else
            {
                await formatter.FormatDataAsync(dataItems, stream, config, logger, cancellationToken);
            }
        }

        // Check if sink supports progress reporting
        if (sink is IProgressAwareComposableDataSink progressAwareSink)
        {
            await progressAwareSink.WriteToTargetAsync(WriteToStream, config, dataSource, logger, progressReporter, cancellationToken);
        }
        else
        {
            await sink.WriteToTargetAsync(WriteToStream, config, dataSource, logger, cancellationToken);
        }
    }

    public IEnumerable<IDataExtensionSettings> GetSettings()
    {
        return ExtensionHelpers.GetCompositeSettings<TFormatter, TSink>();
    }
}