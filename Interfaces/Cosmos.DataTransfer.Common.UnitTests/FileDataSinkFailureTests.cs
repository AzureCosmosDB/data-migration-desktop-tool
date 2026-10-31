using System.Text;
using System.Security.AccessControl;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Security.Principal;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Cosmos.DataTransfer.Common.UnitTests;

[TestClass]
public class FileDataSinkFailureTests
{
    private DirectoryInfo directory = null!;
    private const string Original = "previous complete output";
    private string Destination => Path.Combine(directory.FullName, "output");

    [TestInitialize]
    public void Initialize() => directory = Directory.CreateTempSubdirectory("dmt-output-tests-");

    [TestCleanup]
    public void Cleanup() => directory.Delete(recursive: true);

    public static IEnumerable<object[]> OutputCases =>
        from compression in Enum.GetValues<CompressionEnum>()
        from existing in new[] { false, true }
        select new object[] { compression, existing };

    public static IEnumerable<object[]> FailureCases =>
        from output in OutputCases
        from afterWrite in new[] { false, true }
        select output.Concat(new object[] { afterWrite }).ToArray();

    [TestMethod]
    [DynamicData(nameof(FailureCases))]
    public async Task Overwrite_SourceFailure_PreservesDestinationAndRemovesTemporaryFile(
        CompressionEnum compression, bool existing, bool afterWrite)
    {
        var path = await PrepareAsync(compression, existing);
        var failure = new IOException("Synthetic source read failure");
        var error = await Assert.ThrowsExceptionAsync<IOException>(() => WriteAsync(new FileDataSink(), async stream =>
        {
            AssertUnchanged(path, existing);
            Assert.AreEqual(1, directory.GetFiles(".dmt-*.tmp").Length);
            if (afterWrite)
                await stream.WriteAsync(Encoding.UTF8.GetBytes("partial output"));
            throw failure;
        }, compression));

        Assert.AreSame(failure, error);
        AssertUnchanged(path, existing);
        AssertNoTemporaryFiles();
    }

    [TestMethod]
    [DynamicData(nameof(OutputCases))]
    public async Task Overwrite_Success_PublishesOnlyAfterClosedAndCanBeRead(CompressionEnum compression, bool existing)
    {
        var path = await PrepareAsync(compression, existing);
        await WriteAsync(new FileDataSink(), async stream =>
        {
            AssertUnchanged(path, existing);
            await stream.WriteAsync(Encoding.UTF8.GetBytes("complete replacement"));
            AssertUnchanged(path, existing);
        }, compression);

        using var output = new FileDataSource().ReadFile(path, compression, NullLogger.Instance)!;
        using var reader = new StreamReader(output);
        Assert.AreEqual("complete replacement", await reader.ReadToEndAsync());
        AssertNoTemporaryFiles();
    }

    [TestMethod]
    [DynamicData(nameof(OutputCases))]
    public async Task Overwrite_CancellationAfterWriteEvenIfFormatterIgnoresToken_DoesNotPublish(
        CompressionEnum compression, bool existing)
    {
        var path = await PrepareAsync(compression, existing);
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => WriteAsync(new FileDataSink(), async stream =>
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes("partial output"));
            cancellation.Cancel();
        }, compression, cancellation.Token));

        AssertUnchanged(path, existing);
        AssertNoTemporaryFiles();
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AlreadyCanceled_DoesNotOpenOutputOrCallFormatter(bool append)
    {
        await File.WriteAllTextAsync(Destination, Original);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var sink = new FileDataSink { OpenFile = (_, _, _) => throw new AssertFailedException("Opened canceled output") };
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() =>
            WriteAsync(sink, _ => throw new AssertFailedException("Called canceled formatter"), token: cancellation.Token, append: append));
        AssertUnchanged(Destination, true);
        AssertNoTemporaryFiles();
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Overwrite_CanceledDuringDisposal_DoesNotPublish(bool existing)
    {
        var path = await PrepareAsync(CompressionEnum.None, existing);
        using var cancellation = new CancellationTokenSource();
        var sink = new FileDataSink
        {
            OpenFile = (name, mode, _) => new FaultingFileStream(name, mode) { AfterClose = cancellation.Cancel }
        };
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() =>
            WriteAsync(sink, stream => stream.WriteAsync("done"u8.ToArray()).AsTask(), token: cancellation.Token));
        AssertUnchanged(path, existing);
        AssertNoTemporaryFiles();
    }

    [TestMethod]
    [DynamicData(nameof(OutputCases))]
    public async Task Overwrite_DestinationWriteFailure_DoesNotPublish(CompressionEnum compression, bool existing)
    {
        var path = await PrepareAsync(compression, existing);
        FaultingFileStream? file = null;
        var sink = new FileDataSink
        {
            OpenFile = (name, mode, _) => file = new FaultingFileStream(name, mode) { FailWrites = true }
        };
        await Assert.ThrowsExceptionAsync<IOException>(() =>
            WriteAsync(sink, stream => stream.WriteAsync("partial"u8.ToArray()).AsTask(), compression));

        Assert.IsNotNull(file);
        Assert.IsTrue(file.Closed);
        AssertUnchanged(path, existing);
        AssertNoTemporaryFiles();
    }

    [TestMethod]
    [DynamicData(nameof(OutputCases))]
    public async Task Overwrite_FileDisposalFailure_DoesNotPublish(CompressionEnum compression, bool existing)
    {
        var path = await PrepareAsync(compression, existing);
        var sink = new FileDataSink
        {
            OpenFile = (name, mode, _) => new FaultingFileStream(name, mode) { FailClose = true }
        };
        await Assert.ThrowsExceptionAsync<IOException>(() =>
            WriteAsync(sink, stream => stream.WriteAsync("complete"u8.ToArray()).AsTask(), compression));
        AssertUnchanged(path, existing);
        AssertNoTemporaryFiles();
    }

    [DataTestMethod]
    [DataRow(CompressionEnum.Gzip)]
    [DataRow(CompressionEnum.Brotli)]
    [DataRow(CompressionEnum.Deflate)]
    public async Task Overwrite_CompressorFinalizationFailure_DoesNotPublish(CompressionEnum compression)
    {
        var path = await PrepareAsync(compression, true);
        FaultingFileStream? file = null;
        var sink = new FileDataSink { OpenFile = (name, mode, _) => file = new FaultingFileStream(name, mode) };
        await Assert.ThrowsExceptionAsync<IOException>(() => WriteAsync(sink, async stream =>
        {
            await stream.WriteAsync("complete"u8.ToArray());
            file!.FailWrites = true;
        }, compression));
        Assert.IsTrue(file!.Closed);
        AssertUnchanged(path, true);
        AssertNoTemporaryFiles();
    }

    [TestMethod]
    public async Task Overwrite_SourceAndDisposalFailure_PreservesOriginalExceptionAndLogsSecondFailure()
    {
        await File.WriteAllTextAsync(Destination, Original);
        var logger = new Mock<ILogger>();
        var failure = new IOException("source failure");
        var sink = new FileDataSink
        {
            OpenFile = (name, mode, _) => new FaultingFileStream(name, mode) { FailClose = true }
        };
        var error = await Assert.ThrowsExceptionAsync<IOException>(() =>
            WriteAsync(sink, _ => throw failure, logger: logger.Object));
        Assert.AreSame(failure, error);
        VerifyError(logger, "finalization also failed");
        AssertUnchanged(Destination, true);
        AssertNoTemporaryFiles();
    }

    [TestMethod]
    public async Task Overwrite_CleanupFailure_ReportsTemporaryPathWithoutMaskingSourceFailure()
    {
        await File.WriteAllTextAsync(Destination, Original);
        var logger = new Mock<ILogger>();
        string? temporaryPath = null;
        var sink = new FileDataSink
        {
            OpenFile = (name, mode, _) =>
            {
                temporaryPath = name;
                return new FaultingFileStream(name, mode)
                {
                    AfterClose = () =>
                    {
                        File.Delete(name);
                        Directory.CreateDirectory(name);
                    }
                };
            }
        };
        var failure = new IOException("source failure");
        var error = await Assert.ThrowsExceptionAsync<IOException>(() =>
            WriteAsync(sink, _ => throw failure, logger: logger.Object));
        Assert.AreSame(failure, error);
        VerifyError(logger, $"Could not remove temporary output '{temporaryPath}'");
        AssertUnchanged(Destination, true);
    }

    [TestMethod]
    public async Task Overwrite_PublicationFailure_RemovesTemporaryFile()
    {
        Directory.CreateDirectory(Destination);
        await Assert.ThrowsExceptionAsync<IOException>(() =>
            WriteAsync(new FileDataSink(), stream => stream.WriteAsync("done"u8.ToArray()).AsTask()));
        Assert.IsTrue(Directory.Exists(Destination));
        AssertNoTemporaryFiles();
    }

    [TestMethod]
    public async Task Overwrite_OpenFailure_PreservesDestination()
    {
        await File.WriteAllTextAsync(Destination, Original);
        var sink = new FileDataSink { OpenFile = (_, _, _) => throw new UnauthorizedAccessException("Synthetic open failure") };
        await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(() =>
            WriteAsync(sink, _ => throw new AssertFailedException("Called formatter after open failure")));
        AssertUnchanged(Destination, true);
        AssertNoTemporaryFiles();
    }

    [TestMethod]
    public async Task Overwrite_LockedWindowsDestination_ReplacementFailurePreservesExistingFile()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Windows file sharing prevents replacement; Unix open files do not.");
            return;
        }
        await File.WriteAllTextAsync(Destination, Original);
        using (File.Open(Destination, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await Assert.ThrowsExceptionAsync<IOException>(() =>
                WriteAsync(new FileDataSink(), stream => stream.WriteAsync("done"u8.ToArray()).AsTask()));
        }
        AssertUnchanged(Destination, true);
        AssertNoTemporaryFiles();
    }

    [TestMethod]
    public async Task Overwrite_ReadOnlyDestination_IsNotReplaced()
    {
        await File.WriteAllTextAsync(Destination, Original);
        File.SetAttributes(Destination, FileAttributes.ReadOnly);
        try
        {
            await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(() =>
                WriteAsync(new FileDataSink(), _ => throw new AssertFailedException("Read-only output accepted")));
            AssertUnchanged(Destination, true);
            AssertNoTemporaryFiles();
        }
        finally
        {
            File.SetAttributes(Destination, FileAttributes.Normal);
        }
    }

    [DataTestMethod]
    [DataRow(UnixFileMode.UserRead | UnixFileMode.UserWrite)]
    [DataRow(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead)]
    public async Task Overwrite_UnixPermissions_ArePreserved(UnixFileMode mode)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Unix permission bits are not available on Windows.");
            return;
        }
        await File.WriteAllTextAsync(Destination, Original);
        File.SetUnixFileMode(Destination, mode);
        await WriteAsync(new FileDataSink(), stream =>
        {
            if (!OperatingSystem.IsWindows())
                Assert.AreEqual(mode, File.GetUnixFileMode(((FileStream)stream).Name));
            return stream.WriteAsync("done"u8.ToArray()).AsTask();
        });
        Assert.AreEqual(mode, File.GetUnixFileMode(Destination));
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Overwrite_UnixStaging_IsPrivateAtCreation(bool existing)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Unix creation permissions are not available on Windows.");
            return;
        }
        if (existing)
        {
            await File.WriteAllTextAsync(Destination, Original);
            File.SetUnixFileMode(Destination, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        UnixFileMode? createdMode = null;
        var sink = new FileDataSink
        {
            OpenFile = (name, mode, permissions) =>
            {
                var file = FileDataSink.OpenFileStream(name, mode, permissions);
                if (!OperatingSystem.IsWindows())
                    createdMode = File.GetUnixFileMode(name);
                return file;
            }
        };
        await WriteAsync(sink, stream => stream.WriteAsync("private output"u8.ToArray()).AsTask());
        Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, createdMode);
        Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Destination));
        AssertNoTemporaryFiles();
    }

    [DataTestMethod]
    [DataRow("fifo")]
    [DataRow("socket")]
    public async Task Overwrite_UnixSpecialFile_IsRejectedBeforeStaging(string type)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Unix special files are not available on Windows.");
            return;
        }
        using var socket = type == "socket" ? new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified) : null;
        if (socket != null)
            socket.Bind(new UnixDomainSocketEndPoint(Destination));
        else
        {
            var start = new ProcessStartInfo("mkfifo") { UseShellExecute = false };
            start.ArgumentList.Add(Destination);
            using var process = Process.Start(start)!;
            await process.WaitForExitAsync();
            Assert.AreEqual(0, process.ExitCode, "Could not create the disposable FIFO fixture.");
        }

        Assert.IsFalse(UnixFileSystem.IsRegularFile(Destination));
        var sink = new FileDataSink
        {
            OpenFile = (_, _, _) => throw new AssertFailedException("Special-file validation must precede staging.")
        };
        var error = await Assert.ThrowsExceptionAsync<IOException>(() =>
            WriteAsync(sink, _ => throw new AssertFailedException("Special-file validation must precede formatting.")));
        StringAssert.Contains(error.Message, "special-file destination");
        Assert.IsFalse(UnixFileSystem.IsRegularFile(Destination), "The special file must not be replaced.");
        AssertNoTemporaryFiles();
    }

    [TestMethod]
    public void UnixFileInspection_Failure_IsNotTreatedAsARegularFile()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Unix file inspection is not available on Windows.");
            return;
        }
        var error = Assert.ThrowsException<IOException>(() =>
        {
            if (!OperatingSystem.IsWindows())
                UnixFileSystem.IsRegularFile(Destination);
        });
        StringAssert.Contains(error.Message, Destination);
        Assert.IsInstanceOfType<Win32Exception>(error.InnerException);
    }

    [TestMethod]
    public async Task Overwrite_WindowsAccessControl_IsPreservedDuringStagingAndAfterReplacement()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Windows DACLs are not available on this platform.");
            return;
        }
        await File.WriteAllTextAsync(Destination, Original);
        var info = new FileInfo(Destination);
        var permissions = info.GetAccessControl(AccessControlSections.Access);
        permissions.SetAccessRuleProtection(isProtected: true, preserveInheritance: true);
        info.SetAccessControl(permissions);
        var expected = info.GetAccessControl(AccessControlSections.Access);
        FileSecurity? createdAccessControl = null;
        var sink = new FileDataSink
        {
            OpenFile = (name, mode, security) =>
            {
                var file = FileDataSink.OpenFileStream(name, mode, security);
                if (OperatingSystem.IsWindows())
                    createdAccessControl = new FileInfo(name).GetAccessControl(AccessControlSections.Access);
                return file;
            }
        };
        await WriteAsync(sink, stream => stream.WriteAsync("done"u8.ToArray()).AsTask());
        Assert.IsNotNull(createdAccessControl, "The destination DACL must be supplied at file creation.");
        AssertSameAccess(expected, createdAccessControl);
        AssertSameAccess(expected, info.GetAccessControl(AccessControlSections.Access));
    }

    [SupportedOSPlatform("windows")]
    private static void AssertSameAccess(FileSecurity expected, FileSecurity actual)
    {
        Assert.AreEqual(expected.AreAccessRulesProtected, actual.AreAccessRulesProtected);
        static object[] Rules(FileSecurity security) => security.GetAccessRules(true, true, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .Select(rule => (object)(rule.IdentityReference.Value, rule.FileSystemRights, rule.AccessControlType,
                rule.InheritanceFlags, rule.PropagationFlags)).ToArray();
        CollectionAssert.AreEqual(Rules(expected), Rules(actual));
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Overwrite_SymbolicLink_IsRejectedWithoutChangingLinkOrTarget(bool broken)
    {
        var target = Path.Combine(directory.FullName, "target");
        if (!broken)
            await File.WriteAllTextAsync(target, Original);
        try
        {
            File.CreateSymbolicLink(Destination, target);
        }
        catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Windows symbolic-link creation requires Developer Mode or the symlink privilege.");
            return;
        }
        catch (IOException ex) when (OperatingSystem.IsWindows() && ex.HResult == unchecked((int)0x80070522))
        {
            Assert.Inconclusive("Windows symbolic-link creation requires Developer Mode or the symlink privilege.");
            return;
        }
        var error = await Assert.ThrowsExceptionAsync<IOException>(() =>
            WriteAsync(new FileDataSink(), _ => throw new AssertFailedException("Opened a symbolic-link destination")));
        StringAssert.Contains(error.Message, "symbolic link");
        Assert.AreEqual(target, new FileInfo(Destination).LinkTarget);
        AssertUnchanged(target, !broken);
        AssertNoTemporaryFiles();
    }

    [TestMethod]
    public async Task Overwrite_DoesNotRemoveStaleTemporaryFiles()
    {
        var stale = Path.Combine(directory.FullName, ".dmt-stale.tmp");
        await File.WriteAllTextAsync(stale, "interrupted migration");
        await WriteAsync(new FileDataSink(), stream => stream.WriteAsync("done"u8.ToArray()).AsTask());
        Assert.AreEqual("interrupted migration", await File.ReadAllTextAsync(stale));
        Assert.AreEqual(1, directory.GetFiles(".dmt-*.tmp").Length);
    }

    [DataTestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task Append_Failure_RetainsPartialBytesAndReportsNoRollback(bool canceled, bool existing)
    {
        if (existing)
            await File.WriteAllTextAsync(Destination, Original);
        var logger = new Mock<ILogger>();
        using var cancellation = new CancellationTokenSource();
        Task Write() => WriteAsync(new FileDataSink(), async stream =>
        {
            await stream.WriteAsync("partial"u8.ToArray());
            if (canceled)
                cancellation.Cancel();
            else
                throw new IOException("source failure");
        }, token: cancellation.Token, append: true, logger: logger.Object);
        if (canceled)
            await Assert.ThrowsExceptionAsync<OperationCanceledException>(Write);
        else
            await Assert.ThrowsExceptionAsync<IOException>(Write);

        Assert.AreEqual((existing ? Original : "") + "partial", await File.ReadAllTextAsync(Destination));
        VerifyError(logger, "may contain partial appended data; it has not been rolled back");
        AssertNoTemporaryFiles();
    }

    private IConfiguration Config(CompressionEnum compression, bool append) =>
        TestHelpers.CreateConfig(new Dictionary<string, string>
        {
            ["FilePath"] = Destination,
            ["Compression"] = compression.ToString(),
            ["Append"] = append.ToString()
        });

    private Task WriteAsync(FileDataSink sink, Func<Stream, Task> writer, CompressionEnum compression = CompressionEnum.None,
        CancellationToken token = default, bool append = false, ILogger? logger = null) =>
        sink.WriteToTargetAsync(writer, Config(compression, append), Mock.Of<IDataSourceExtension>(), logger ?? NullLogger.Instance, token);

    private async Task<string> PrepareAsync(CompressionEnum compression, bool existing)
    {
        var path = Destination + (compression switch
        {
            CompressionEnum.Gzip => ".gz",
            CompressionEnum.Deflate => ".zz",
            CompressionEnum.Brotli => ".br",
            _ => ""
        });
        if (existing)
            await File.WriteAllTextAsync(path, Original);
        return path;
    }

    private static void AssertUnchanged(string path, bool existing)
    {
        Assert.AreEqual(existing, File.Exists(path));
        if (existing)
            Assert.AreEqual(Original, File.ReadAllText(path));
    }

    private void AssertNoTemporaryFiles() => Assert.AreEqual(0, directory.GetFileSystemInfos(".dmt-*.tmp").Length);

    private static void VerifyError(Mock<ILogger> logger, string message) =>
        logger.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(message)),
            It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once());

    private sealed class FaultingFileStream(string path, FileMode mode) : FileStream(path, mode, FileAccess.Write)
    {
        public bool FailWrites { get; set; }
        public bool FailClose { get; init; }
        public bool Closed { get; private set; }
        public Action? AfterClose { get; init; }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (FailWrites) throw new IOException("Synthetic output write failure");
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (FailWrites) throw new IOException("Synthetic output write failure");
            base.Write(buffer);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (FailWrites) throw new IOException("Synthetic output write failure");
            return base.WriteAsync(buffer, cancellationToken);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (FailWrites) throw new IOException("Synthetic output write failure");
            return base.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            Closed = true;
            AfterClose?.Invoke();
            if (FailClose) throw new IOException("Synthetic output finalization failure");
        }
    }
}
