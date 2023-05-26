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

        async Task WriteToStream(Stream stream)
        {
            await formatter.FormatDataAsync(dataItems, stream, config, logger, cancellationToken);
        }

        await sink.WriteToTargetAsync(WriteToStream, config, dataSource, logger, cancellationToken);
    }

    public IEnumerable<IDataExtensionSettings> GetSettings()
    {
        return ExtensionHelpers.GetCompositeSettings<TFormatter, TSink>();
    }
}