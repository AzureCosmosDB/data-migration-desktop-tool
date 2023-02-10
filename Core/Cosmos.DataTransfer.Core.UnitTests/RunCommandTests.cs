using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.CommandLine;
using System;
using System.CommandLine.Invocation;
using System.Collections.Generic;
using Moq;
using System.ComponentModel.Composition.Hosting;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace Cosmos.DataTransfer.Core.UnitTests
{
    [TestClass]
    public class RunCommandTests
    {
        [TestMethod]
        public void Invoke_WithSingleConfig_ExecutesSingleOperation()
        {
            const string source = "testSource";
            const string sink = "testSink";
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "Source", source },
                    { "Sink", sink },
                })
                .Build();
            var loader = new Mock<IExtensionLoader>();
            var sourceExtension = new Mock<IDataSourceExtension>();
            sourceExtension.SetupGet(ds => ds.DisplayName).Returns(source);
            loader
                .Setup(l => l.LoadExtensions<IDataSourceExtension>(It.IsAny<CompositionContainer>()))
                .Returns(new List<IDataSourceExtension> { sourceExtension.Object });

            var sinkExtension = new Mock<IDataSinkExtension>();
            sinkExtension.SetupGet(ds => ds.DisplayName).Returns(sink);
            loader
                .Setup(l => l.LoadExtensions<IDataSinkExtension>(It.IsAny<CompositionContainer>()))
                .Returns(new List<IDataSinkExtension> { sinkExtension.Object });
            var handler = new RunCommand.CommandHandler(loader.Object,
                configuration,
                NullLoggerFactory.Instance);

            var parseResult = new RootCommand().Parse(Array.Empty<string>());
            var result = handler.Invoke(new InvocationContext(parseResult));
            Assert.AreEqual(0, result);

            sourceExtension.Verify(se => se.ReadAsync(It.IsAny<IConfiguration>(), It.IsAny<ILogger>(), It.IsAny<CancellationToken>()), Times.Once);
            sinkExtension.Verify(se => se.WriteAsync(It.IsAny <IAsyncEnumerable<IDataItem>>(), It.IsAny<IConfiguration>(), sourceExtension.Object, It.IsAny<ILogger>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public void Invoke_WithMultipleOperations_ExecutesAllOperations()
        {
            const string source = "testSource";
            const string sink = "testSink";
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "Source", source },
                    { "Sink", sink },
                    { "Operations:0:SourceSettings:FilePath", "file-in.json" },
                    { "Operations:0:SinkSettings:FilePath", "file-out.json" },
                    { "Operations:1:SourceSettings:FilePath", "file1.json" },
                    { "Operations:1:SinkSettings:FilePath", "file2.json" },
                    { "Operations:2:SourceSettings:FilePath", "fileA.json" },
                    { "Operations:2:SinkSettings:FilePath", "fileB.json" },
                })
                .Build();
            var loader = new Mock<IExtensionLoader>();
            var sourceExtension = new Mock<IDataSourceExtension>();
            sourceExtension.SetupGet(ds => ds.DisplayName).Returns(source);
            loader
                .Setup(l => l.LoadExtensions<IDataSourceExtension>(It.IsAny<CompositionContainer>()))
                .Returns(new List<IDataSourceExtension> { sourceExtension.Object });

            var sinkExtension = new Mock<IDataSinkExtension>();
            sinkExtension.SetupGet(ds => ds.DisplayName).Returns(sink);
            loader
                .Setup(l => l.LoadExtensions<IDataSinkExtension>(It.IsAny<CompositionContainer>()))
                .Returns(new List<IDataSinkExtension> { sinkExtension.Object });
            var handler = new RunCommand.CommandHandler(loader.Object,
                configuration,
                NullLoggerFactory.Instance);

            var parseResult = new RootCommand().Parse(Array.Empty<string>());
            var result = handler.Invoke(new InvocationContext(parseResult));
            Assert.AreEqual(0, result);

            sourceExtension.Verify(se => se.ReadAsync(It.IsAny<IConfiguration>(), It.IsAny<ILogger>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
            sinkExtension.Verify(se => se.WriteAsync(It.IsAny<IAsyncEnumerable<IDataItem>>(), It.IsAny<IConfiguration>(), sourceExtension.Object, It.IsAny<ILogger>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        }

        [TestMethod]
        public void Invoke_WithMultipleSinks_ExecutesAllOperationsFromSource()
        {
            const string source = "testSource";
            const string sink = "testSink";
            const string sourceFile = "file-in.json";
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "Source", source },
                    { "Sink", sink },
                    { "SourceSettings:FilePath", sourceFile },
                    { "Operations:0:SinkSettings:FilePath", "file-out.json" },
                    { "Operations:1:SinkSettings:FilePath", "file2.json" },
                    { "Operations:2:SinkSettings:FilePath", "fileB.json" },
                })
                .Build();
            var loader = new Mock<IExtensionLoader>();
            var sourceExtension = new Mock<IDataSourceExtension>();
            sourceExtension.SetupGet(ds => ds.DisplayName).Returns(source);
            loader
                .Setup(l => l.LoadExtensions<IDataSourceExtension>(It.IsAny<CompositionContainer>()))
                .Returns(new List<IDataSourceExtension> { sourceExtension.Object });

            var sinkExtension = new Mock<IDataSinkExtension>();
            sinkExtension.SetupGet(ds => ds.DisplayName).Returns(sink);
            loader
                .Setup(l => l.LoadExtensions<IDataSinkExtension>(It.IsAny<CompositionContainer>()))
                .Returns(new List<IDataSinkExtension> { sinkExtension.Object });
            var handler = new RunCommand.CommandHandler(loader.Object,
                configuration,
                NullLoggerFactory.Instance);

            var parseResult = new RootCommand().Parse(Array.Empty<string>());
            var result = handler.Invoke(new InvocationContext(parseResult));
            Assert.AreEqual(0, result);

            sourceExtension.Verify(se => se.ReadAsync(It.Is<IConfiguration>(c => c["FilePath"] == sourceFile), It.IsAny<ILogger>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
            sinkExtension.Verify(se => se.WriteAsync(It.IsAny<IAsyncEnumerable<IDataItem>>(), It.IsAny<IConfiguration>(), sourceExtension.Object, It.IsAny<ILogger>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        }

        [TestMethod]
        public void Invoke_WithMultipleSources_ExecutesAllOperationsToSink()
        {
            const string source = "testSource";
            const string sink = "testSink";
            const string targetFile = "file-out.json";
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "Source", source },
                    { "Sink", sink },
                    { "SinkSettings:FilePath", targetFile },
                    { "Operations:0:SourceSettings:FilePath", "file-in.json" },
                    { "Operations:1:SourceSettings:FilePath", "file1.json" },
                    { "Operations:2:SourceSettings:FilePath", "fileA.json" },
                })
                .Build();
            var loader = new Mock<IExtensionLoader>();
            var sourceExtension = new Mock<IDataSourceExtension>();
            sourceExtension.SetupGet(ds => ds.DisplayName).Returns(source);
            loader
                .Setup(l => l.LoadExtensions<IDataSourceExtension>(It.IsAny<CompositionContainer>()))
                .Returns(new List<IDataSourceExtension> { sourceExtension.Object });

            var sinkExtension = new Mock<IDataSinkExtension>();
            sinkExtension.SetupGet(ds => ds.DisplayName).Returns(sink);
            loader
                .Setup(l => l.LoadExtensions<IDataSinkExtension>(It.IsAny<CompositionContainer>()))
                .Returns(new List<IDataSinkExtension> { sinkExtension.Object });
            var handler = new RunCommand.CommandHandler(loader.Object,
                configuration,
                NullLoggerFactory.Instance);

            var parseResult = new RootCommand().Parse(Array.Empty<string>());
            var result = handler.Invoke(new InvocationContext(parseResult));
            Assert.AreEqual(0, result);

            sourceExtension.Verify(se => se.ReadAsync(It.IsAny<IConfiguration>(), It.IsAny<ILogger>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
            sinkExtension.Verify(se => se.WriteAsync(It.IsAny<IAsyncEnumerable<IDataItem>>(), It.Is<IConfiguration>(c => c["FilePath"] == targetFile), sourceExtension.Object, It.IsAny<ILogger>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        }
    }
}