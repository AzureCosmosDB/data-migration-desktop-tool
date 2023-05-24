using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.Interfaces;

public interface IComposableDataSource : IExtensionWithSettings
{
    IAsyncEnumerable<Stream?> ReadSourceAsync(IConfiguration config, ILogger logger, CancellationToken cancellationToken = default);
}