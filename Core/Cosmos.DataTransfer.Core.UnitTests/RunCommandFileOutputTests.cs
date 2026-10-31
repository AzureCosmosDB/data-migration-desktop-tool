using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cosmos.DataTransfer.Common;
using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.JsonExtension;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Cosmos.DataTransfer.Core.UnitTests;

[TestClass]
public class RunCommandFileOutputTests
{
    private DirectoryInfo directory = null!;
    private const string Original = "[{\"id\":\"original\"}]";
    private string Input => Path.Combine(directory.FullName, "source.json");
    private string Output => Path.Combine(directory.FullName, "output.json");

    [TestInitialize]
    public void Initialize() => directory = Directory.CreateTempSubdirectory("dmt-cli-output-tests-");

    [TestCleanup]
    public void Cleanup() => directory.Delete(recursive: true);

    public static IEnumerable<object[]> MigrationCases =>
        from scenario in new[] { "missing", "early", "late", "valid", "empty" }
        from existing in new[] { false, true }
        from compression in new[] { "None", "Gzip", "Brotli", "Deflate" }
        select new object[] { scenario, existing, compression };

    [TestMethod]
    [DynamicData(nameof(MigrationCases))]
    public async Task InvokeAsync_RealJsonPipeline_ExitCodeAndPublicationMatchResult(string scenario, bool existing, string compression)
    {
        if (scenario != "missing")
        {
            var input = scenario switch
            {
                "early" => "{\"id\":}",
                "late" => "[" + string.Concat(Enumerable.Repeat("{\"id\":1,\"text\":\"" + new string('x', 100) + "\"},", 3000)) + "{\"id\":}]",
                "valid" => "[{\"id\":1},{\"id\":2}]",
                _ => "[]"
            };
            await File.WriteAllTextAsync(Input, input);
        }
        var output = Output + Extension(compression);
        if (existing)
            await File.WriteAllTextAsync(output, Original);

        int consumed = 0;
        var realSource = new JsonFileSource();
        var source = new Mock<IDataSourceExtension>();
        source.SetupGet(s => s.DisplayName).Returns("JSON");
        source.Setup(s => s.ReadAsync(It.IsAny<IConfiguration>(), It.IsAny<ILogger>(), It.IsAny<CancellationToken>()))
            .Returns((IConfiguration config, ILogger logger, CancellationToken token) =>
                CountItems(realSource.ReadAsync(config, logger, token), () => consumed++, token));
        var logger = new Mock<ILogger>();
        var result = await InvokeAsync(source.Object, Input, Output, compression, logger.Object);
        bool success = scenario is "valid" or "empty";

        Assert.AreEqual(success ? 0 : 1, result);
        Assert.AreEqual(success || existing, File.Exists(output));
        Assert.AreEqual(0, directory.GetFiles(".dmt-*.tmp").Length);
        if (success)
        {
            using var file = File.OpenRead(output);
            using var decompressor = Decompress(file, compression);
            using var document = await JsonDocument.ParseAsync(decompressor);
            Assert.AreEqual(scenario == "valid" ? 2 : 0, document.RootElement.GetArrayLength());
        }
        else if (existing)
        {
            Assert.AreEqual(Original, await File.ReadAllTextAsync(output));
        }
        if (scenario == "late")
            Assert.IsTrue(consumed > 0, "The malformed-source case must fail after records have reached the real JSON writer.");
        logger.Verify(l => l.Log(LogLevel.Information, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString() == "Data transfer complete"),
            null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()), success ? Times.Once() : Times.Never());
    }

    [TestMethod]
    public async Task InvokeAsync_DestinationPublicationFailure_ReturnsNonzeroAndRemovesTemporaryFile()
    {
        await File.WriteAllTextAsync(Input, "[{\"id\":1}]");
        Directory.CreateDirectory(Output);
        Assert.AreEqual(1, await InvokeAsync(new JsonFileSource(), Input, Output));
        Assert.IsTrue(Directory.Exists(Output));
        Assert.AreEqual(0, directory.GetFiles(".dmt-*.tmp").Length);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task InvokeAsync_SameSourceAndDestination_DoesNotTruncateSource(bool malformed)
    {
        var input = malformed ? "[{\"id\":}]" : Original;
        await File.WriteAllTextAsync(Input, input);
        Assert.AreEqual(malformed ? 1 : 0, await InvokeAsync(new JsonFileSource(), Input, Input));
        if (malformed)
            Assert.AreEqual(input, await File.ReadAllTextAsync(Input));
        else
        {
            using var result = JsonDocument.Parse(await File.ReadAllTextAsync(Input));
            Assert.AreEqual("original", result.RootElement[0].GetProperty("id").GetString());
        }
        Assert.AreEqual(0, directory.GetFiles(".dmt-*.tmp").Length);
    }

    [TestMethod]
    public async Task InvokeAsync_DirectorySource_DoesNotConsumeSiblingStagingFile()
    {
        await File.WriteAllTextAsync(Input, Original);
        Assert.AreEqual(0, await InvokeAsync(new JsonFileSource(), directory.FullName, Output));
        using var result = JsonDocument.Parse(await File.ReadAllTextAsync(Output));
        Assert.AreEqual(1, result.RootElement.GetArrayLength());
        Assert.AreEqual(0, directory.GetFiles(".dmt-*.tmp").Length);
    }

    [TestMethod]
    public async Task InvokeAsync_SourceCancellation_ReturnsNonzeroAndPreservesDestination()
    {
        await File.WriteAllTextAsync(Output, Original);
        var source = new Mock<IDataSourceExtension>();
        source.SetupGet(s => s.DisplayName).Returns("JSON");
        source.Setup(s => s.ReadAsync(It.IsAny<IConfiguration>(), It.IsAny<ILogger>(), It.IsAny<CancellationToken>()))
            .Returns(CanceledItems());
        Assert.AreEqual(1, await InvokeAsync(source.Object, Input, Output));
        Assert.AreEqual(Original, await File.ReadAllTextAsync(Output));
        Assert.AreEqual(0, directory.GetFiles(".dmt-*.tmp").Length);
    }

    private static async IAsyncEnumerable<IDataItem> CanceledItems()
    {
        await Task.Yield();
        yield return new DictionaryDataItem(new Dictionary<string, object?> { ["id"] = 1 });
        throw new OperationCanceledException("Synthetic source cancellation");
    }

    private static async IAsyncEnumerable<IDataItem> CountItems(IAsyncEnumerable<IDataItem> items, Action count,
        [EnumeratorCancellation] CancellationToken token)
    {
        await foreach (var item in items.WithCancellation(token))
        {
            count();
            yield return item;
        }
    }

    private static async Task<int> InvokeAsync(IDataSourceExtension source, string input, string output,
        string compression = "None", ILogger? logger = null)
    {
        var loader = new Mock<IExtensionLoader>();
        loader.Setup(l => l.LoadExtensions<IDataSourceExtension>(It.IsAny<CompositionContainer>()))
            .Returns(new List<IDataSourceExtension> { source });
        loader.Setup(l => l.LoadExtensions<IDataSinkExtension>(It.IsAny<CompositionContainer>()))
            .Returns(new List<IDataSinkExtension> { new JsonFileSink() });
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(logger ?? NullLogger.Instance);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SourceSettings:FilePath"] = input,
            ["SinkSettings:FilePath"] = output,
            ["SinkSettings:BufferSizeMB"] = "0",
            ["SinkSettings:Compression"] = compression
        }).Build();
        var handler = new RunCommand.CommandHandler(loader.Object, config, loggerFactory.Object)
        {
            Source = "JSON",
            Sink = "JSON"
        };
        return await handler.InvokeAsync(new InvocationContext(new RootCommand().Parse(Array.Empty<string>())));
    }

    private static string Extension(string compression) => compression switch
    {
        "Gzip" => ".gz",
        "Brotli" => ".br",
        "Deflate" => ".zz",
        _ => ""
    };

    private static Stream Decompress(Stream file, string compression) => compression switch
    {
        "Gzip" => new GZipStream(file, CompressionMode.Decompress, leaveOpen: true),
        "Brotli" => new BrotliStream(file, CompressionMode.Decompress, leaveOpen: true),
        "Deflate" => new DeflateStream(file, CompressionMode.Decompress, leaveOpen: true),
        _ => file
    };
}
