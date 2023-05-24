using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.Common
{
    public abstract class CompositeSourceExtension<TSource, TFormatter> : IDataSourceExtensionWithSettings
        where TSource : class, IComposableDataSource, new()
        where TFormatter : class, IFormattedDataReader, new()
    {
        public abstract string DisplayName { get; }

        public IAsyncEnumerable<IDataItem> ReadAsync(IConfiguration config, ILogger logger, CancellationToken cancellationToken = default)
        {
            var source = new TSource();
            var formatter = new TFormatter();

            return formatter.ParseDataAsync(source, config, logger, cancellationToken);
        }

        public IEnumerable<IDataExtensionSettings> GetSettings()
        {
            return ExtensionHelpers.GetCompositeSettings<TFormatter, TSource>();
        }
    }
}