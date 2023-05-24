using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.Interfaces
{
    public interface IDataSourceExtension : IDataTransferExtension
    {
        IAsyncEnumerable<IDataItem> ReadAsync(IConfiguration config, ILogger logger, CancellationToken cancellationToken = default);
    }

    public interface IDataSourceExtensionWithSettings : IDataSourceExtension, IExtensionWithSettings
    {
    }
}
