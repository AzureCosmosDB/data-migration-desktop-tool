using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.Interfaces;

public interface IFormattedDataReader : IExtensionWithSettings
{
    IAsyncEnumerable<IDataItem> ParseDataAsync(IComposableDataSource sourceExtension, IConfiguration config, ILogger logger, CancellationToken cancellationToken = default);
}