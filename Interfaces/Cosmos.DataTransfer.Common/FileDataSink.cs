using System.IO.Compression;
using System.Security.AccessControl;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.Common;

public class FileDataSink : IComposableDataSink
{
    internal Func<string, FileMode, byte[]?, Stream> OpenFile { get; init; } = OpenFileStream;

    public async Task WriteToTargetAsync(Func<Stream, Task> writeToStream, IConfiguration config, IDataSourceExtension dataSource, ILogger logger, CancellationToken cancellationToken = default)
    {
        var settings = config.Get<FileSinkSettings>();
        settings.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        var destination = Path.GetFullPath(GetOutputPath(settings!.FilePath!, settings.Compression));
        if (settings.Append)
        {
            try
            {
                await WriteAndCloseAsync(OpenFile(destination, FileMode.Append, null), settings.Compression, writeToStream, logger);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Append to '{FilePath}' failed. The destination may contain partial appended data; it has not been rolled back.", destination);
                throw;
            }
            return;
        }

        // A non-format extension also keeps staging files out of directory-source globbing.
        var temporaryPath = Path.Combine(Path.GetDirectoryName(destination)!, $".dmt-{Guid.NewGuid():N}.tmp");
        bool temporaryCreated = false;
        try
        {
            ValidateDestination(destination);
            byte[]? permissions = OperatingSystem.IsWindows() && File.Exists(destination)
                ? new FileInfo(destination).GetAccessControl(AccessControlSections.Access).GetSecurityDescriptorBinaryForm()
                : null;
            var file = OpenFile(temporaryPath, FileMode.CreateNew, permissions);
            temporaryCreated = true;
            await WriteAndCloseAsync(file, settings.Compression, async writer =>
            {
                if (!OperatingSystem.IsWindows() && File.Exists(destination))
                    File.SetUnixFileMode(temporaryPath, File.GetUnixFileMode(destination));
                await writeToStream(writer);
            }, logger);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateDestination(destination);

            if (File.Exists(destination))
            {
                File.Replace(temporaryPath, destination, null);
            }
            else
            {
                // Do not overwrite a destination created concurrently with this migration.
                File.Move(temporaryPath, destination);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "File output to '{FilePath}' failed; no completed output was published.", destination);
            throw;
        }
        finally
        {
            if (temporaryCreated)
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    logger.LogError(ex, "Could not remove temporary output '{TemporaryPath}'. It is not a completed migration output; remove it manually.", temporaryPath);
                }
            }
        }
    }

    public IEnumerable<IDataExtensionSettings> GetSettings()
    {
        yield return new FileSinkSettings();
    }

    internal static Stream OpenFileStream(string path, FileMode mode, byte[]? permissions)
    {
        if (OperatingSystem.IsWindows() && permissions != null)
        {
            var security = new FileSecurity();
            security.SetSecurityDescriptorBinaryForm(permissions, AccessControlSections.Access);
            return new FileInfo(path).Create(mode, FileSystemRights.Write, FileShare.None,
                4096, FileOptions.None, security);
        }

        var options = new FileStreamOptions { Mode = mode, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows() && mode == FileMode.CreateNew)
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        return new FileStream(path, options);
    }

    private static void ValidateDestination(string destination)
    {
        var info = new FileInfo(destination);
        if (info.LinkTarget != null || (info.Exists && info.Attributes.HasFlag(FileAttributes.ReparsePoint)))
            throw new IOException($"Overwrite output cannot replace a symbolic link or reparse point: '{destination}'. Use a regular destination file.");
        if (!OperatingSystem.IsWindows() && info.Exists && !UnixFileSystem.IsRegularFile(destination))
            throw new IOException($"Overwrite output requires a regular file; special-file destination '{destination}' will not be replaced.");
        if (info.Exists && info.IsReadOnly)
            throw new UnauthorizedAccessException($"Overwrite output cannot replace a read-only file: '{destination}'.");
    }

    private static async Task WriteAndCloseAsync(Stream file, CompressionEnum compression, Func<Stream, Task> writeToStream, ILogger logger)
    {
        Stream writer = file;
        Exception? failure = null;
        try
        {
            writer = compression switch
            {
                CompressionEnum.Deflate => new DeflateStream(file, CompressionMode.Compress, leaveOpen: true),
                CompressionEnum.Gzip => new GZipStream(file, CompressionMode.Compress, leaveOpen: true),
                CompressionEnum.Brotli => new BrotliStream(file, CompressionMode.Compress, leaveOpen: true),
                _ => file
            };
            await writeToStream(writer);
        }
        catch (Exception ex)
        {
            failure = ex;
            throw;
        }
        finally
        {
            try
            {
                await CloseAsync(writer, failure, logger);
            }
            catch (Exception ex)
            {
                failure = ex;
                throw;
            }
            finally
            {
                if (!ReferenceEquals(writer, file))
                    await CloseAsync(file, failure, logger);
            }
        }
    }

    private static async ValueTask CloseAsync(Stream stream, Exception? failure, ILogger logger)
    {
        try
        {
            await stream.DisposeAsync();
        }
        catch (Exception ex) when (failure != null)
        {
            // Preserve the original migration error while reporting a second finalization failure.
            logger.LogError(ex, "Output stream finalization also failed.");
        }
    }

    private static string GetOutputPath(string path, CompressionEnum compression) => compression switch
    {
        CompressionEnum.Deflate => EnsureExtension(path, ".zz"),
        CompressionEnum.Gzip => EnsureExtension(path, ".gz", ".gzip"),
        CompressionEnum.Brotli => EnsureExtension(path, ".br"),
        _ => path
    };

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