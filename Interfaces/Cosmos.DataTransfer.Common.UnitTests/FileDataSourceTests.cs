using System.Text;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Moq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cosmos.DataTransfer.Common.UnitTests;

[TestClass]
public class FileSinkDataSourceTests {

    [TestMethod]
    public async Task ReadSourceAsync_MissingFile_ThrowsWithPath()
    {
        var path = $"{Guid.NewGuid():N}.json";
        var config = TestHelpers.CreateConfig(new() { { "FilePath", path } });
        var error = await Assert.ThrowsExceptionAsync<FileNotFoundException>(async () =>
            await new FileDataSource().ReadSourceAsync(config, NullLogger.Instance).ToListAsync());
        StringAssert.Contains(error.FileName!, path);
    }

    [TestMethod]
    public async Task ReadSourceAsync_EmptyDirectory_ReturnsNoStreams()
    {
        var directory = GetTemporaryDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "ignored.txt"), "not a matching file");
            var config = TestHelpers.CreateConfig(new() { { "FilePath", directory } });
            var streams = await new FileDataSource().ReadSourceAsync(config, NullLogger.Instance).ToListAsync();
            Assert.AreEqual(0, streams.Count);
        }
        finally
        {
            File.Delete(Path.Combine(directory, "ignored.txt"));
            Directory.Delete(directory);
        }
    }

    [DataTestMethod]
    [DataRow(HttpStatusCode.NotFound)]
    [DataRow(HttpStatusCode.Forbidden)]
    [DataRow(HttpStatusCode.InternalServerError)]
    public async Task ReadSourceAsync_HttpError_ThrowsWithStatusAndSafeLocation(HttpStatusCode status)
    {
        await using var server = new HttpTestServer(status, "not JSON");
        var uri = new UriBuilder(server.Url) { UserName = "private", Password = "private", Query = "token=private", Fragment = "private" };
        var config = TestHelpers.CreateConfig(new() { { "FilePath", uri.Uri.AbsoluteUri } });
        var logger = new Mock<ILogger>();
        var error = await Assert.ThrowsExceptionAsync<HttpRequestException>(async () =>
            await new FileDataSource().ReadSourceAsync(config, logger.Object).ToListAsync());
        Assert.AreEqual(status, error.StatusCode);
        StringAssert.Contains(error.Message, server.Url);
        Assert.IsFalse(error.ToString().Contains("private"));
        logger.Verify(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("private")),
            It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);
    }

    [TestMethod]
    public async Task ReadSourceAsync_UnreachableHttp_ThrowsWithLocation()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var url = $"http://127.0.0.1:{((IPEndPoint)socket.LocalEndPoint!).Port}/source.json";
        var config = TestHelpers.CreateConfig(new() { { "FilePath", url } });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var error = await Assert.ThrowsExceptionAsync<HttpRequestException>(async () =>
            await new FileDataSource().ReadSourceAsync(config, NullLogger.Instance, timeout.Token).ToListAsync());
        StringAssert.Contains(error.Message, url);
        Assert.IsInstanceOfType<HttpRequestException>(error.InnerException);
    }

    [TestMethod]
    public async Task ReadSourceAsync_HttpBody_NullByteArrayBuffer_ThrowsEvenForZeroByteReads()
    {
        await using var server = new HttpTestServer(HttpStatusCode.OK, "body");
        var config = TestHelpers.CreateConfig(new() { { "FilePath", server.Url } });
        using var guard = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var streams = new FileDataSource().ReadSourceAsync(config, NullLogger.Instance, guard.Token).GetAsyncEnumerator();
        Assert.IsTrue(await streams.MoveNextAsync());
        var stream = streams.Current!;
        Assert.ThrowsException<ArgumentNullException>(() => stream.Read(null!, 0, 0));
        await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () =>
            await stream.ReadAsync(null!, 0, 0));
    }

    [DataTestMethod]
    [DataRow("memory")]
    [DataRow("array")]
    [DataRow("sync")]
    public async Task ReadSourceAsync_StalledHttpBody_TimesOutWithSafeLocation(string readMethod)
    {
        await using var server = new HttpTestServer(HttpStatusCode.OK, "body",
            (_, cancellation) => Task.Delay(Timeout.InfiniteTimeSpan, cancellation));
        var uri = new UriBuilder(server.Url) { UserName = "private", Password = "private", Query = "token=private", Fragment = "private" };
        var config = TestHelpers.CreateConfig(new() { { "FilePath", uri.Uri.AbsoluteUri } });
        var source = new FileDataSource { HttpReadTimeout = TimeSpan.FromMilliseconds(200) };
        using var guard = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var streams = source.ReadSourceAsync(config, NullLogger.Instance, guard.Token).GetAsyncEnumerator();
        Assert.IsTrue(await streams.MoveNextAsync());
        var stream = streams.Current!;
        var buffer = new byte[1];
        var error = await Assert.ThrowsExceptionAsync<HttpRequestException>(async () =>
        {
            if (readMethod == "sync")
                await Task.Run(() => stream.Read(buffer, 0, buffer.Length));
            else if (readMethod == "array")
                await stream.ReadAsync(buffer, 0, buffer.Length, guard.Token);
            else
                await stream.ReadAsync(buffer.AsMemory(), guard.Token);
        });
        StringAssert.Contains(error.Message, "Timed out");
        StringAssert.Contains(error.Message, server.Url);
        Assert.IsFalse(error.ToString().Contains("private"));
        Assert.IsFalse(guard.IsCancellationRequested);
        await streams.DisposeAsync();
        Assert.IsFalse(stream.CanRead);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReadSourceAsync_StalledHttpBody_HonorsReadAndMigrationCancellation(bool cancelMigration)
    {
        await using var server = new HttpTestServer(HttpStatusCode.OK, "body",
            (_, cancellation) => Task.Delay(Timeout.InfiniteTimeSpan, cancellation));
        var config = TestHelpers.CreateConfig(new() { { "FilePath", server.Url } });
        var source = new FileDataSource { HttpReadTimeout = TimeSpan.FromSeconds(5) };
        using var migrationCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var readCancellation = new CancellationTokenSource();
        await using var streams = source.ReadSourceAsync(config, NullLogger.Instance, migrationCancellation.Token).GetAsyncEnumerator();
        Assert.IsTrue(await streams.MoveNextAsync());
        var stream = streams.Current!;
        var read = stream.ReadAsync(new byte[1],
            cancelMigration ? CancellationToken.None : readCancellation.Token).AsTask();
        (cancelMigration ? migrationCancellation : readCancellation).Cancel();
        try
        {
            await read;
            Assert.Fail("The body read should have been canceled.");
        }
        catch (OperationCanceledException)
        {
        }
        await streams.DisposeAsync();
        Assert.IsFalse(stream.CanRead);
    }

    [TestMethod]
    public async Task ReadSourceAsync_ProgressingHttpBody_CanExceedReadTimeout()
    {
        const string content = "0123456789abcdef";
        await using var server = new HttpTestServer(HttpStatusCode.OK, content, async (stream, cancellation) =>
        {
            foreach (byte value in Encoding.UTF8.GetBytes(content))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellation);
                await stream.WriteAsync(new[] { value }, cancellation);
            }
        });
        var config = TestHelpers.CreateConfig(new() { { "FilePath", server.Url } });
        var source = new FileDataSource { HttpReadTimeout = TimeSpan.FromSeconds(1) };
        using var guard = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var streams = source.ReadSourceAsync(config, NullLogger.Instance, guard.Token).GetAsyncEnumerator();
        Assert.IsTrue(await streams.MoveNextAsync());
        using var received = new MemoryStream();
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        await streams.Current!.CopyToAsync(received, guard.Token);
        Assert.AreEqual(content, Encoding.UTF8.GetString(received.ToArray()));
        Assert.IsTrue(elapsed.Elapsed > source.HttpReadTimeout);
        Assert.IsFalse(await streams.MoveNextAsync());
    }

    [TestMethod]
    public async Task ReadSourceAsync_HttpBody_ProcessingPauseDoesNotConsumeReadTimeout()
    {
        await using var server = new HttpTestServer(HttpStatusCode.OK, "body");
        var config = TestHelpers.CreateConfig(new() { { "FilePath", server.Url } });
        var source = new FileDataSource { HttpReadTimeout = TimeSpan.FromMilliseconds(200) };
        using var guard = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var streams = source.ReadSourceAsync(config, NullLogger.Instance, guard.Token).GetAsyncEnumerator();
        Assert.IsTrue(await streams.MoveNextAsync());
        var stream = streams.Current!;
        var buffer = new byte[1];
        Assert.AreEqual(1, await stream.ReadAsync(buffer.AsMemory(), guard.Token));
        Assert.AreEqual((byte)'b', buffer[0]);
        await Task.Delay(TimeSpan.FromMilliseconds(500), guard.Token);
        using var received = new MemoryStream();
        await stream.CopyToAsync(received, guard.Token);
        Assert.AreEqual("ody", Encoding.UTF8.GetString(received.ToArray()));
        await streams.DisposeAsync();
        Assert.IsFalse(stream.CanRead);
    }

    [TestMethod]
    public async Task ReadSourceAsync_LockedFile_PropagatesIOException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        try
        {
            using var locked = File.Open(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            var config = TestHelpers.CreateConfig(new() { { "FilePath", path } });
            await Assert.ThrowsExceptionAsync<IOException>(async () =>
                await new FileDataSource().ReadSourceAsync(config, NullLogger.Instance).ToListAsync());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task ReadSourceAsync_MissingParent_ThrowsWithPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}", "missing.json");
        var config = TestHelpers.CreateConfig(new() { { "FilePath", path } });
        var error = await Assert.ThrowsExceptionAsync<DirectoryNotFoundException>(async () =>
            await new FileDataSource().ReadSourceAsync(config, NullLogger.Instance).ToListAsync());
        StringAssert.Contains(error.Message, path);
    }

    [TestMethod]
    public async Task ReadSourceAsync_Canceled_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var config = TestHelpers.CreateConfig(new() { { "FilePath", "Data/foobar.txt" } });
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () =>
            await new FileDataSource().ReadSourceAsync(config, NullLogger.Instance, cancellation.Token).ToListAsync());
    }

    public static IEnumerable<object[]> Test_ReadFileData { get {
        yield return new object[] { "Data/foobar.txt", CompressionEnum.None };
        yield return new object[] { "Data/foobar.txt.br", CompressionEnum.None };
        yield return new object[] { "Data/foobar.txt.gz", CompressionEnum.None };
        yield return new object[] { "Data/foobar.txt.gzip", CompressionEnum.None };
        yield return new object[] { "Data/foobar.txt.zz", CompressionEnum.None };
        yield return new object[] { "Data/foobar.txt.br", CompressionEnum.Brotli};
        yield return new object[] { "Data/foobar.txt.gz", CompressionEnum.Gzip };
        yield return new object[] { "Data/foobar.txt.gzip", CompressionEnum.Gzip };
        yield return new object[] { "Data/foobar.txt.zz", CompressionEnum.Deflate };
    } }

    [TestMethod]
    [DynamicData(nameof(Test_ReadFileData))]
    public async Task Test_ReadFile(string filePath, CompressionEnum compression, string expected = "Hello world!") {
        var fileSource = new FileDataSource();
        var reader = fileSource.ReadFile(filePath, compression, NullLogger.Instance)!;
        var buffer = new byte[100];
        await reader.ReadAsync(buffer.AsMemory(0, buffer.Length));
        var result = Encoding.UTF8.GetString(buffer);
        Assert.AreEqual(expected, result.TrimEnd('\0'), $"file: {filePath}, compression: {compression}.");
    }

    [TestMethod]
    [DynamicData(nameof(Test_ReadFileData))]
    public async Task Test_ReadSourceAsync_SingleFile(string filePath, CompressionEnum compression, string expected = "Hello world!") {
        var fileSource = new FileDataSource();
        var config = TestHelpers.CreateConfig(new Dictionary<string,string>() {
            { "FilePath", filePath },
            { "Compression", compression.ToString() }
        });

        var streams = await fileSource.ReadSourceAsync(config, NullLogger.Instance).ToListAsync();
        Assert.AreEqual(1, streams.Count);
        var stream = streams!.First();
        var buffer = new byte[100];
        await stream!.ReadAsync(buffer.AsMemory(0, buffer.Length));
        var result = Encoding.UTF8.GetString(buffer);
        Assert.AreEqual(expected, result.TrimEnd('\0'), $"file: {filePath}, compression: {compression}.");
    }

    internal static string GetTemporaryDirectory() {
        string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        if (File.Exists(tempDirectory)) 
        {
            return GetTemporaryDirectory();
        } else 
        {
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }
    }

    [TestMethod]
    public async Task Test_ReadSourceAsync_Directory() {
        var files = new string[] {
            "rich.json", "rich.json.gz", "rich.json.gzip"
        };
        var tmpdir = GetTemporaryDirectory();
        foreach (var f in files) {
            File.Copy(Path.Combine("Data", f), Path.Combine(tmpdir, f));
        }
        var fileSource = new FileDataSource();
        var logs = new List<string>();
        var config = TestHelpers.CreateConfig(new Dictionary<string,string>() {
            { "FilePath", tmpdir }
        });

        var result = await fileSource.ReadSourceAsync(config, NullLogger.Instance)
            .Select(async (Stream? stream, CancellationToken cancellationToken) => {
                var buffer = new byte[100];
                await stream!.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                return Encoding.UTF8.GetString(buffer).TrimEnd('\0');
            }).ToArrayAsync();
        var expected = new string[files.Length];
        Array.Fill(expected, "[{\"id\":1,\"name\":\"john\"}]");
        CollectionAssert.AreEqual(expected, result);
    }

    [TestMethod]
    public void Test_GetSettings() {
        var sink = new FileDataSource();
        var response = sink.GetSettings().ToArray();
        Assert.AreEqual(1, response.Length);
        Assert.IsInstanceOfType(response[0], typeof(FileSourceSettings));
    }
}