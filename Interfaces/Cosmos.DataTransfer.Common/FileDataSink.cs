using System.IO.Compression;
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
            if (settings.Gzip && !settings.FilePath.EndsWith(".gz")) {
                settings.FilePath += ".gz";
            }

            await using var writer = File.Create(settings.FilePath);
            
            if (settings.Gzip) {
                using var compressor = new GZipStream(writer, CompressionLevel.SmallestSize, leaveOpen: false);
                await writeToStream(compressor);
            } else {
                await writeToStream(writer);
            }
        }
    }

    public IEnumerable<IDataExtensionSettings> GetSettings()
    {
        yield return new FileSinkSettings();
    }
}