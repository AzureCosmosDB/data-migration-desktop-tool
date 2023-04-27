using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.Common;

public abstract class CompositeSinkExtension<TSink, TFormatter> : IDataSinkExtension
    where TSink : class, IComposableDataSink, new()
    where TFormatter : class, IFormattedDataWriter, new()
{
    public abstract string DisplayName { get; }

    public async Task WriteAsync(IAsyncEnumerable<IDataItem> dataItems, IConfiguration config, IDataSourceExtension dataSource, ILogger logger, CancellationToken cancellationToken = default)
    {
        var sink = new TSink();
        var formatter = new TFormatter();

        await sink.WriteToTargetAsync(formatter, dataItems, config, dataSource, logger, cancellationToken);
    }
}