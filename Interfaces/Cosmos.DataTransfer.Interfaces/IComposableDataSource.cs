using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.Interfaces;

public interface IComposableDataSource
{
    IAsyncEnumerable<Stream?> ReadSourceAsync(IConfiguration config, ILogger logger, CancellationToken cancellationToken = default);
}