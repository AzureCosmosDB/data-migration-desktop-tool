using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.Common;

public class FileDataSink : IComposableDataSink
{
    public async Task WriteToTargetAsync(Func<Stream, Task> writeToStream, IConfiguration config, IDataSourceExtension dataSource, ILogger logger, CancellationToken cancellationToken = default)
    {
        var settings = config.Get<FileSinkSettings>();
        settings.Validate();
        if (settings.FilePath != null)
        {
            await using var writer = File.Create(settings.FilePath);
            await writeToStream(writer);
        }
    }

    public IEnumerable<IDataExtensionSettings> GetSettings()
    {
        yield return new FileSinkSettings();
    }
}