using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Cosmos.DataTransfer.Core.UnitTests;

[TestClass]
public class RunCommandSourceFailureTests
{
    [DataTestMethod]
    [DataRow("missing", "sink")]
    [DataRow("http", "sink")]
    [DataRow("json", "sink")]
    [DataRow("empty", "sink")]
    [DataRow("missing", "Cosmos-nosql")]
    [DataRow("http", "Cosmos-nosql")]
    [DataRow("json", "Cosmos-nosql")]
    [DataRow("empty", "Cosmos-nosql")]
    public async Task InvokeAsync_EnumeratesSource_ReportsFailureOrValidEmptySuccess(string scenario, string sinkName)
    {
        Exception? failure = scenario switch
        {
            "missing" => new FileNotFoundException("Source file was not found.", "missing.json"),
            "http" => new HttpRequestException("HTTP source returned 404.", null, HttpStatusCode.NotFound),
            "json" => new JsonException("Invalid JSON.", "$.name", 2, 10),
            _ => null
        };
        var source = new Mock<IDataSourceExtension>();
        source.SetupGet(s => s.DisplayName).Returns("source");
        source.Setup(s => s.ReadAsync(It.IsAny<IConfiguration>(), It.IsAny<ILogger>(), It.IsAny<CancellationToken>()))
            .Returns(EnumerateAsync(failure));
        var sink = new Mock<IDataSinkExtension>();
        sink.SetupGet(s => s.DisplayName).Returns(sinkName);
        int consumed = 0;
        sink.Setup(s => s.WriteAsync(It.IsAny<IAsyncEnumerable<IDataItem>>(), It.IsAny<IConfiguration>(),
                source.Object, It.IsAny<ILogger>(), It.IsAny<CancellationToken>()))
            .Returns(async (IAsyncEnumerable<IDataItem> items, IConfiguration config, IDataSourceExtension extension,
                ILogger logger, CancellationToken cancellation) =>
            {
                await foreach (var item in items.WithCancellation(cancellation))
                    consumed++;
            });

        var loader = new Mock<IExtensionLoader>();
        loader.Setup(l => l.LoadExtensions<IDataSourceExtension>(It.IsAny<CompositionContainer>()))
            .Returns(new List<IDataSourceExtension> { source.Object });
        loader.Setup(l => l.LoadExtensions<IDataSinkExtension>(It.IsAny<CompositionContainer>()))
            .Returns(new List<IDataSinkExtension> { sink.Object });
        var logger = new Mock<ILogger>();
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(logger.Object);
        var handler = new RunCommand.CommandHandler(loader.Object, new ConfigurationBuilder().Build(), loggerFactory.Object)
        {
            Source = "source",
            Sink = sinkName
        };
        var result = await handler.InvokeAsync(new InvocationContext(new RootCommand().Parse(Array.Empty<string>())));

        Assert.AreEqual(failure == null ? 0 : 1, result);
        Assert.AreEqual(failure == null ? 0 : 1, consumed);
        logger.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString() == "Data transfer failed"),
            failure, It.IsAny<Func<It.IsAnyType, Exception?, string>>()), failure == null ? Times.Never() : Times.Once());
        logger.Verify(l => l.Log(LogLevel.Information, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString() == "Data transfer complete"),
            null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()), failure == null ? Times.Once() : Times.Never());
    }

    private static async IAsyncEnumerable<IDataItem> EnumerateAsync(Exception? failure)
    {
        await Task.Yield();
        if (failure != null)
        {
            yield return Mock.Of<IDataItem>();
            throw failure;
        }
    }
}
