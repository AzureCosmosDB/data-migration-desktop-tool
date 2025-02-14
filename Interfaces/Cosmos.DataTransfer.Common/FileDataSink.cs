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
            using var writer = GetCompressor(settings.Compression, settings.FilePath, settings.Append);
            await writeToStream(writer);
            writer.Close();
        }
    }

    public IEnumerable<IDataExtensionSettings> GetSettings()
    {
        yield return new FileSinkSettings();
    }

    private static Stream GetCompressor(CompressionEnum compression, string filepath, bool append = false) {
        FileMode fileMode = append ? FileMode.Append : FileMode.Create;
        Func<FileStream, Stream> compressor;
        switch (compression) {
            case CompressionEnum.Deflate:
                filepath = EnsureExtension(filepath, ".zz");
                compressor = (x) => new DeflateStream(x, CompressionMode.Compress, leaveOpen: false);
                break;
            case CompressionEnum.Gzip:
                filepath = EnsureExtension(filepath, ".gz", ".gzip");
                compressor = (x) => new GZipStream(x, CompressionMode.Compress, leaveOpen: false);
                break;
            case CompressionEnum.Brotli:
                filepath = EnsureExtension(filepath, ".br");
                compressor = (x) => new BrotliStream(x, CompressionMode.Compress, leaveOpen: false);
                break;            
            case CompressionEnum.None:
            default:
                compressor = (x) => x;
                break;
        }

        var writer = File.Open(filepath, fileMode, FileAccess.Write);
        return compressor(writer);
    }

    private static string EnsureExtension(string fn, params string[] extensions) {
        bool found = false;
        foreach (var ext in extensions) {
            found = fn.EndsWith(ext);
            if (found) break;
        }
        if (!found) {
            fn += extensions[0];
        }
        return fn;
    }
}