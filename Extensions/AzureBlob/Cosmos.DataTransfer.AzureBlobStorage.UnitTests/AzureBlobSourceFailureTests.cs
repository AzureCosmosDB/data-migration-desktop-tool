using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel.Composition.Hosting;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Cosmos.DataTransfer.Core;
using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.JsonExtension;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Cosmos.DataTransfer.AzureBlobStorage.UnitTests;

[TestClass]
public class AzureBlobSourceFailureTests
{
    private const string Secret = "private-test-value";

    [TestMethod]
    public async Task ReadSourceAsync_MissingNamedBlob_ThrowsWithSafeContext()
    {
        var (source, blob) = CreateSource("missing");
        var logger = new RecordingLogger();
        var error = await Assert.ThrowsExceptionAsync<FileNotFoundException>(async () =>
            await source.ReadSourceAsync(CreateConfig(), logger).ToListAsync());

        StringAssert.Contains(error.Message, "source.json");
        StringAssert.Contains(error.Message, "input");
        StringAssert.Contains(error.Message, "not found");
        AssertSafe(error.ToString());
        AssertSafe(string.Join("\n", logger.Messages));
        blob.Verify(b => b.OpenReadAsync(It.IsAny<BlobOpenReadOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ReadSourceAsync_SignedUrlInLocation_DoesNotExposeCredentials()
    {
        var (source, _) = CreateSource("missing");
        var config = new ConfigurationBuilder().AddConfiguration(CreateConfig())
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BlobName"] = $"https://user:{Secret}@example.invalid/input/source.json?sig={Secret}#{Secret}",
                ["ContainerName"] = $"input?sig={Secret}"
            }).Build();
        var logger = new RecordingLogger();
        var error = await Assert.ThrowsExceptionAsync<FileNotFoundException>(async () =>
            await source.ReadSourceAsync(config, logger).ToListAsync());
        StringAssert.Contains(error.Message, "https://example.invalid/input/source.json");
        AssertSafe(error.ToString());
        AssertSafe(string.Join("\n", logger.Messages));
    }

    [DataTestMethod]
    [DataRow("argument")]
    [DataRow("uri")]
    [DataRow("authentication")]
    [DataRow("http")]
    public async Task ReadSourceAsync_ClientFailure_IsActionableWithoutCredentialDetails(string scenario)
    {
        Exception failure = scenario switch
        {
            "argument" => new ArgumentException(Secret),
            "uri" => new UriFormatException(Secret),
            "authentication" => new AuthenticationFailedException(Secret),
            _ => new HttpRequestException(Secret)
        };
        var source = new AzureBlobDataSource(_ => throw failure);
        var error = await Assert.ThrowsExceptionAsync<IOException>(async () =>
            await source.ReadSourceAsync(CreateConfig(), NullLogger.Instance).ToListAsync());
        StringAssert.Contains(error.Message, "source.json");
        StringAssert.Contains(error.Message, "input");
        AssertSafe(error.ToString());
    }

    [DataTestMethod]
    [DataRow("missing")]
    [DataRow("exists-forbidden")]
    [DataRow("open-forbidden")]
    [DataRow("deleted-before-open")]
    [DataRow("body-service-error")]
    [DataRow("body-io-error")]
    [DataRow("body-http-error")]
    public async Task JsonPipeline_UnavailableSource_ThrowsRatherThanWritingEmptySuccess(string scenario)
    {
        var (source, _) = CreateSource(scenario);
        var config = CreateConfig();
        using var output = new MemoryStream();
        var items = new JsonFormatReader().ParseDataAsync(source, config, NullLogger.Instance);
        Task WriteAsync() => new JsonFormatWriter().FormatDataAsync(items, output, config, NullLogger.Instance);
        IOException error = scenario == "missing"
            ? await Assert.ThrowsExceptionAsync<FileNotFoundException>(WriteAsync)
            : await Assert.ThrowsExceptionAsync<IOException>(WriteAsync);

        StringAssert.Contains(error.Message, "source.json");
        StringAssert.Contains(error.Message, "input");
        if (scenario.Contains("forbidden"))
            StringAssert.Contains(error.Message, "403");
        if (scenario == "deleted-before-open")
            StringAssert.Contains(error.Message, "404");
        AssertSafe(error.ToString());
    }

    [DataTestMethod]
    [DataRow("memory")]
    [DataRow("array")]
    [DataRow("sync")]
    public async Task ReadSourceAsync_DeferredReadFailure_IsSafeAndDisposesStream(string readMethod)
    {
        var (source, _) = CreateSource("body-service-error");
        var streams = await source.ReadSourceAsync(CreateConfig(), NullLogger.Instance).ToListAsync();
        await using var stream = streams.Single()!;
        var buffer = new byte[1];
        var error = await Assert.ThrowsExceptionAsync<IOException>(async () =>
        {
            if (readMethod == "sync")
                Assert.AreEqual(1, stream.Read(buffer, 0, buffer.Length));
            else if (readMethod == "array")
                Assert.AreEqual(1, await stream.ReadAsync(buffer, 0, buffer.Length));
            else
                Assert.AreEqual(1, await stream.ReadAsync(buffer.AsMemory()));
        });
        StringAssert.Contains(error.Message, "503");
        AssertSafe(error.ToString());
        await stream.DisposeAsync();
        Assert.IsFalse(stream.CanRead);
    }

    [TestMethod]
    public async Task ReadSourceAsync_ValidStream_PreservesSeekingAndDisposal()
    {
        var (source, blob) = CreateSource("valid");
        var config = new ConfigurationBuilder().AddConfiguration(CreateConfig())
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ReadBufferSizeInKB"] = "4096" }).Build();
        var streams = await source.ReadSourceAsync(config, NullLogger.Instance).ToListAsync();
        blob.Verify(b => b.OpenReadAsync(It.Is<BlobOpenReadOptions>(o => o.BufferSize == 4096),
            It.IsAny<CancellationToken>()), Times.Once);
        using var stream = streams.Single()!;
        Assert.IsTrue(stream.CanSeek);
        Assert.AreEqual(14L, stream.Length);
        Assert.AreEqual('[', (char)stream.ReadByte());
        stream.Position = 0;
        Assert.AreEqual(0L, stream.Position);
        Assert.AreEqual('[', (char)stream.ReadByte());
        Assert.AreEqual(0L, stream.Seek(0, SeekOrigin.Begin));
        using var reader = new StreamReader(stream);
        Assert.AreEqual("[{\"id\":\"one\"}]", await reader.ReadToEndAsync());
        stream.Dispose();
        Assert.IsFalse(stream.CanRead);
    }

    [TestMethod]
    public async Task ReadSourceAsync_Canceled_DoesNotCreateClient()
    {
        var source = new AzureBlobDataSource(_ => throw new AssertFailedException("No client should be created."));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () =>
            await source.ReadSourceAsync(CreateConfig(), NullLogger.Instance, cancellation.Token).ToListAsync());
    }

    [DataTestMethod]
    [DataRow("exists")]
    [DataRow("open")]
    [DataRow("read")]
    public async Task ReadSourceAsync_OperationCancellation_Propagates(string stage)
    {
        using var cancellation = new CancellationTokenSource();
        var failure = new OperationCanceledException(cancellation.Token);
        var blob = new Mock<BlockBlobClient>(MockBehavior.Strict);
        if (stage == "exists")
            blob.Setup(b => b.ExistsAsync(cancellation.Token)).ThrowsAsync(failure);
        else
        {
            blob.Setup(b => b.ExistsAsync(cancellation.Token)).ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));
            if (stage == "open")
                blob.Setup(b => b.OpenReadAsync(It.IsAny<BlobOpenReadOptions>(), cancellation.Token)).ThrowsAsync(failure);
            else
                blob.Setup(b => b.OpenReadAsync(It.IsAny<BlobOpenReadOptions>(), cancellation.Token))
                    .ReturnsAsync(new FailingReadStream(failure));
        }
        var source = new AzureBlobDataSource(_ => blob.Object);
        var error = await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () =>
        {
            await foreach (var stream in source.ReadSourceAsync(CreateConfig(), NullLogger.Instance, cancellation.Token))
            {
                await using var owned = stream!;
                Assert.AreEqual(1, await owned.ReadAsync(new byte[1], cancellation.Token));
            }
        });
        Assert.AreSame(failure, error);
    }

    [DataTestMethod]
    [DataRow("missing", 1, -1)]
    [DataRow("exists-forbidden", 1, -1)]
    [DataRow("open-forbidden", 1, -1)]
    [DataRow("deleted-before-open", 1, -1)]
    [DataRow("body-service-error", 1, -1)]
    [DataRow("body-io-error", 1, -1)]
    [DataRow("body-http-error", 1, -1)]
    [DataRow("empty", 0, 0)]
    [DataRow("valid", 0, 1)]
    public async Task InvokeAsync_RealBlobJsonPipeline_ReportsFailureOrValidSuccess(string scenario, int exitCode, int count)
    {
        var (blobSource, _) = CreateSource(scenario);
        var source = new Mock<IDataSourceExtension>();
        source.SetupGet(s => s.DisplayName).Returns("JSON-AzureBlob(beta)");
        source.Setup(s => s.ReadAsync(It.IsAny<IConfiguration>(), It.IsAny<ILogger>(), It.IsAny<CancellationToken>()))
            .Returns((IConfiguration config, ILogger logger, CancellationToken cancellation) =>
                new JsonFormatReader().ParseDataAsync(blobSource, config, logger, cancellation));
        var sink = new JsonFileSink();
        var loader = new Mock<IExtensionLoader>();
        loader.Setup(l => l.LoadExtensions<IDataSourceExtension>(It.IsAny<CompositionContainer>()))
            .Returns(new List<IDataSourceExtension> { source.Object });
        loader.Setup(l => l.LoadExtensions<IDataSinkExtension>(It.IsAny<CompositionContainer>()))
            .Returns(new List<IDataSinkExtension> { sink });
        var logger = new RecordingLogger();
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(logger);
        var outputPath = Path.Combine(Path.GetTempPath(), $"blob-source-{Guid.NewGuid():N}.json");
        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(CreateConfig().AsEnumerable()
                    .Select(kv => new KeyValuePair<string, string?>($"SourceSettings:{kv.Key}", kv.Value)))
                .AddInMemoryCollection(new Dictionary<string, string?> { ["SinkSettings:FilePath"] = outputPath })
                .Build();
            var handler = new RunCommand.CommandHandler(loader.Object, config, loggerFactory.Object)
            {
                Source = source.Object.DisplayName,
                Sink = sink.DisplayName
            };
            int result = await handler.InvokeAsync(new InvocationContext(new RootCommand().Parse(Array.Empty<string>())));

            Assert.AreEqual(exitCode, result);
            Assert.AreEqual(exitCode == 0, logger.Messages.Any(m => m == "Data transfer complete"));
            Assert.AreEqual(exitCode != 0, logger.Messages.Any(m => m.StartsWith("Data transfer failed")));
            AssertSafe(string.Join("\n", logger.Messages));
            if (exitCode == 0)
            {
                using var output = JsonDocument.Parse(await File.ReadAllTextAsync(outputPath));
                Assert.AreEqual(count, output.RootElement.GetArrayLength());
                if (count > 0)
                    Assert.AreEqual("one", output.RootElement[0].GetProperty("id").GetString());
            }
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    private static IConfiguration CreateConfig() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionString"] = $"AccountKey={Secret}",
            ["UseRbacAuth"] = "true",
            ["AccountEndpoint"] = $"https://user:{Secret}@example.invalid/?sig={Secret}#{Secret}",
            ["ContainerName"] = "input",
            ["BlobName"] = $"source.json?sig={Secret}#{Secret}"
        }).Build();

    private static (AzureBlobDataSource Source, Mock<BlockBlobClient> Blob) CreateSource(string scenario)
    {
        var blob = new Mock<BlockBlobClient>(MockBehavior.Strict);
        var exists = blob.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>()));
        if (scenario == "exists-forbidden")
            exists.ThrowsAsync(new RequestFailedException(403, Secret));
        else
            exists.ReturnsAsync(Response.FromValue(scenario != "missing", Mock.Of<Response>()));
        var open = blob.Setup(b => b.OpenReadAsync(It.IsAny<BlobOpenReadOptions>(), It.IsAny<CancellationToken>()));
        if (scenario is "open-forbidden" or "deleted-before-open")
            open.ThrowsAsync(new RequestFailedException(scenario == "open-forbidden" ? 403 : 404, Secret));
        else if (scenario.StartsWith("body-"))
        {
            Exception failure = scenario switch
            {
                "body-service-error" => new RequestFailedException(503, Secret),
                "body-http-error" => new HttpRequestException(Secret),
                _ => new IOException(Secret)
            };
            open.ReturnsAsync(new FailingReadStream(failure));
        }
        else
            open.ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes(scenario == "valid" ? "[{\"id\":\"one\"}]" : "[]")));
        return (new AzureBlobDataSource(_ => blob.Object), blob);
    }

    private static void AssertSafe(string text)
    {
        Assert.IsFalse(text.Contains(Secret), text);
        Assert.IsFalse(text.Contains("?sig="), text);
        Assert.IsFalse(text.Contains("AccountKey="), text);
    }

    private sealed class FailingReadStream(Exception failure) : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) => throw failure;
        public override int Read(Span<byte> buffer) => throw failure;
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Task.FromException<int>(failure);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(failure);
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Messages { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
            if (exception != null)
                Messages.Add(exception.ToString());
        }
    }
}
