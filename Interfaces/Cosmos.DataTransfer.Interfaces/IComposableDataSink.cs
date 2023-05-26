using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.Interfaces;

public interface IComposableDataSink : IExtensionWithSettings
{
    Task WriteToTargetAsync(Func<Stream, Task> writeToStream, IConfiguration config, IDataSourceExtension dataSource, ILogger logger, CancellationToken cancellationToken = default);
}