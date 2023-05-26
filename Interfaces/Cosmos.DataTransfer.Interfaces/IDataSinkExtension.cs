using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.Interfaces
{
    public interface IDataSinkExtension : IDataTransferExtension
    {
        Task WriteAsync(IAsyncEnumerable<IDataItem> dataItems, IConfiguration config, IDataSourceExtension dataSource, ILogger logger, CancellationToken cancellationToken = default);
    }
    public interface IDataSinkExtensionWithSettings : IDataSinkExtension, IExtensionWithSettings
    {
    }
}
