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
using System.Threading.Tasks;

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
        public async Task InvokeAsync_WithMultipleAccounts_AwaitsEachSinkAndRereadsSourceWithIndependentSettings()
        {
            const string sourceName = "JSON";
            const string sinkName = "Cosmos-nosql";
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "SourceSettings:FilePath", "source.json" },
                    { "SinkSettings:PartitionKeyPath", "/id" },
                    { "SinkSettings:WriteMode", "UpsertStream" },
                    { "SinkSettings:AllowBulkExecution", "false" },
                    { "Operations:0:SinkSettings:ConnectionString", "account1-connection" },
                    { "Operations:0:SinkSettings:Database", "db1" },
                    { "Operations:0:SinkSettings:Container", "container1" },
                    { "Operations:0:SinkSettings:AllowBulkExecution", "true" },
                    { "Operations:1:SinkSettings:ConnectionString", "account2-connection" },
                    { "Operations:1:SinkSettings:Database", "db2" },
                    { "Operations:1:SinkSettings:Container", "container2" }
                }).Build();
            var events = new List<string>();
            var sinkConfigs = new List<IConfiguration>();
            var sourcePaths = new List<string?>();
            var releaseFirstSink = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var item = new Mock<IDataItem>().Object;

            async IAsyncEnumerable<IDataItem> ReadSource(IConfiguration config)
            {
                sourcePaths.Add(config["FilePath"]);
                events.Add("read");
                yield return item;
                await Task.CompletedTask;
            }

            var source = new Mock<IDataSourceExtension>();
            source.SetupGet(s => s.DisplayName).Returns(sourceName);
            source.Setup(s => s.ReadAsync(It.IsAny<IConfiguration>(), It.IsAny<ILogger>(), It.IsAny<CancellationToken>()))
                .Returns((IConfiguration config, ILogger logger, CancellationToken token) => ReadSource(config));
            var sink = new Mock<IDataSinkExtension>();
            sink.SetupGet(s => s.DisplayName).Returns(sinkName);
            sink.Setup(s => s.WriteAsync(It.IsAny<IAsyncEnumerable<IDataItem>>(), It.IsAny<IConfiguration>(),
                    source.Object, It.IsAny<ILogger>(), It.IsAny<CancellationToken>()))
                .Returns(async (IAsyncEnumerable<IDataItem> data, IConfiguration config,
                    IDataSourceExtension dataSource, ILogger logger, CancellationToken token) =>
                {
                    sinkConfigs.Add(config);
                    await foreach (var record in data)
                    {
                        Assert.AreSame(item, record);
                        events.Add($"write:{config["Database"]}");
                    }
                    if (config["Database"] == "db1")
                    {
                        await releaseFirstSink.Task;
                    }
                    events.Add($"complete:{config["Database"]}");
                });
            var loader = new Mock<IExtensionLoader>();
            loader.Setup(l => l.LoadExtensions<IDataSourceExtension>(It.IsAny<CompositionContainer>()))
                .Returns(new List<IDataSourceExtension> { source.Object });
            loader.Setup(l => l.LoadExtensions<IDataSinkExtension>(It.IsAny<CompositionContainer>()))
                .Returns(new List<IDataSinkExtension> { sink.Object });
            var handler = new RunCommand.CommandHandler(loader.Object, configuration, NullLoggerFactory.Instance)
            {
                Source = sourceName,
                Sink = sinkName
            };

            var run = handler.InvokeAsync(new InvocationContext(new RootCommand().Parse(Array.Empty<string>())));
            try
            {
                Assert.IsFalse(run.IsCompleted, "The command must await the first sink.");
                Assert.AreEqual(1, sinkConfigs.Count, "The second sink must not start before the first completes.");
                Assert.AreEqual(1, sourcePaths.Count, "The source must not be reread until the first operation completes.");
            }
            finally
            {
                releaseFirstSink.TrySetResult();
            }

            Assert.AreEqual(0, await run.WaitAsync(TimeSpan.FromSeconds(5)));
            CollectionAssert.AreEqual(new[] { "read", "write:db1", "complete:db1", "read", "write:db2", "complete:db2" }, events);
            CollectionAssert.AreEqual(new[] { "source.json", "source.json" }, sourcePaths);
            Assert.AreEqual(2, sinkConfigs.Count);
            for (int i = 0; i < sinkConfigs.Count; i++)
            {
                Assert.AreEqual($"account{i + 1}-connection", sinkConfigs[i]["ConnectionString"]);
                Assert.AreEqual($"db{i + 1}", sinkConfigs[i]["Database"]);
                Assert.AreEqual($"container{i + 1}", sinkConfigs[i]["Container"]);
                Assert.AreEqual("/id", sinkConfigs[i]["PartitionKeyPath"]);
                Assert.AreEqual("UpsertStream", sinkConfigs[i]["WriteMode"]);
            }
            Assert.IsTrue(sinkConfigs[0].GetValue<bool>("AllowBulkExecution"));
            Assert.IsFalse(sinkConfigs[1].GetValue<bool>("AllowBulkExecution"), "First-operation overrides must not leak into later operations.");
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

        [TestMethod]
        public void Invoke_WithEmptySourceAndSink_ReturnsError()
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Source", "" },
                    { "Sink", "" },
                })
                .Build();
            var loader = new Mock<IExtensionLoader>();
            loader
                .Setup(l => l.LoadExtensions<IDataSourceExtension>(It.IsAny<CompositionContainer>()))
                .Returns(new List<IDataSourceExtension>());
            loader
                .Setup(l => l.LoadExtensions<IDataSinkExtension>(It.IsAny<CompositionContainer>()))
                .Returns(new List<IDataSinkExtension>());

            var handler = new RunCommand.CommandHandler(loader.Object,
                configuration,
                NullLoggerFactory.Instance);

            var parseResult = new RootCommand().Parse(Array.Empty<string>());
            var result = handler.Invoke(new InvocationContext(parseResult));
            
            // Should return error code when source/sink are not configured
            Assert.AreEqual(1, result);
        }

        [TestMethod]
        public void Invoke_WithMissingSource_ReturnsError()
        {
            const string sink = "testSink";
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Sink", sink },
                })
                .Build();
            var loader = new Mock<IExtensionLoader>();
            var sinkExtension = new Mock<IDataSinkExtension>();
            sinkExtension.SetupGet(ds => ds.DisplayName).Returns(sink);
            loader
                .Setup(l => l.LoadExtensions<IDataSourceExtension>(It.IsAny<CompositionContainer>()))
                .Returns(new List<IDataSourceExtension>());
            loader
                .Setup(l => l.LoadExtensions<IDataSinkExtension>(It.IsAny<CompositionContainer>()))
                .Returns(new List<IDataSinkExtension> { sinkExtension.Object });

            var handler = new RunCommand.CommandHandler(loader.Object,
                configuration,
                NullLoggerFactory.Instance);

            var parseResult = new RootCommand().Parse(Array.Empty<string>());
            var result = handler.Invoke(new InvocationContext(parseResult));
            
            // Should return error code when source is not configured
            Assert.AreEqual(1, result);
        }

        [TestMethod]
        public void Invoke_WithMissingSink_ReturnsError()
        {
            const string source = "testSource";
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Source", source },
                })
                .Build();
            var loader = new Mock<IExtensionLoader>();
            var sourceExtension = new Mock<IDataSourceExtension>();
            sourceExtension.SetupGet(ds => ds.DisplayName).Returns(source);
            loader
                .Setup(l => l.LoadExtensions<IDataSourceExtension>(It.IsAny<CompositionContainer>()))
                .Returns(new List<IDataSourceExtension> { sourceExtension.Object });
            loader
                .Setup(l => l.LoadExtensions<IDataSinkExtension>(It.IsAny<CompositionContainer>()))
                .Returns(new List<IDataSinkExtension>());

            var handler = new RunCommand.CommandHandler(loader.Object,
                configuration,
                NullLoggerFactory.Instance);

            var parseResult = new RootCommand().Parse(Array.Empty<string>());
            var result = handler.Invoke(new InvocationContext(parseResult));
            
            // Should return error code when sink is not configured
            Assert.AreEqual(1, result);
        }

        [TestMethod]
        public void Invoke_WithInvalidSourceExtension_ThrowsException()
        {
            const string source = "invalidSource";
            const string sink = "testSink";
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Source", source },
                    { "Sink", sink },
                })
                .Build();
            var loader = new Mock<IExtensionLoader>();
            var sourceExtension = new Mock<IDataSourceExtension>();
            sourceExtension.SetupGet(ds => ds.DisplayName).Returns("differentSource");
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
            
            // Should throw exception when source extension is not found
            Assert.ThrowsException<InvalidOperationException>(() => handler.Invoke(new InvocationContext(parseResult)));
        }

        [TestMethod]
        public void Invoke_WithInvalidSinkExtension_ThrowsException()
        {
            const string source = "testSource";
            const string sink = "invalidSink";
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
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
            sinkExtension.SetupGet(ds => ds.DisplayName).Returns("differentSink");
            loader
                .Setup(l => l.LoadExtensions<IDataSinkExtension>(It.IsAny<CompositionContainer>()))
                .Returns(new List<IDataSinkExtension> { sinkExtension.Object });

            var handler = new RunCommand.CommandHandler(loader.Object,
                configuration,
                NullLoggerFactory.Instance);

            var parseResult = new RootCommand().Parse(Array.Empty<string>());
            
            // Should throw exception when sink extension is not found
            Assert.ThrowsException<InvalidOperationException>(() => handler.Invoke(new InvocationContext(parseResult)));
        }

        [TestMethod]
        public void Invoke_WithSameCosmosSourceAndSinkWithRecreateContainer_ThrowsException()
        {
            const string cosmosExtension = "Cosmos-nosql";
            const string connectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=test";
            const string database = "testDb";
            const string container = "testContainer";
            
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "Source", cosmosExtension },
                    { "Sink", cosmosExtension },
                    { "SourceSettings:ConnectionString", connectionString },
                    { "SourceSettings:Database", database },
                    { "SourceSettings:Container", container },
                    { "SinkSettings:ConnectionString", connectionString },
                    { "SinkSettings:Database", database },
                    { "SinkSettings:Container", container },
                    { "SinkSettings:RecreateContainer", "true" },
                })
                .Build();
            
            var loader = new Mock<IExtensionLoader>();
            var sourceExtension = new Mock<IDataSourceExtension>();
            sourceExtension.SetupGet(ds => ds.DisplayName).Returns(cosmosExtension);
            loader
                .Setup(l => l.LoadExtensions<IDataSourceExtension>(It.IsAny<CompositionContainer>()))
                .Returns(new List<IDataSourceExtension> { sourceExtension.Object });

            var sinkExtension = new Mock<IDataSinkExtension>();
            sinkExtension.SetupGet(ds => ds.DisplayName).Returns(cosmosExtension);
            loader
                .Setup(l => l.LoadExtensions<IDataSinkExtension>(It.IsAny<CompositionContainer>()))
                .Returns(new List<IDataSinkExtension> { sinkExtension.Object });
            
            var handler = new RunCommand.CommandHandler(loader.Object,
                configuration,
                NullLoggerFactory.Instance);

            var parseResult = new RootCommand().Parse(Array.Empty<string>());
            
            // Should throw exception when same container is used for source and sink with RecreateContainer
            var exception = Assert.ThrowsException<InvalidOperationException>(() => handler.Invoke(new InvocationContext(parseResult)));
            Assert.IsTrue(exception.Message.Contains("same Cosmos DB container"));
            Assert.IsTrue(exception.Message.Contains("RecreateContainer"));
        }

        [TestMethod]
        public void Invoke_WithSameCosmosSourceAndSinkWithoutRecreateContainer_Succeeds()
        {
            const string cosmosExtension = "Cosmos-nosql";
            const string connectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=test";
            const string database = "testDb";
            const string container = "testContainer";
            
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "Source", cosmosExtension },
                    { "Sink", cosmosExtension },
                    { "SourceSettings:ConnectionString", connectionString },
                    { "SourceSettings:Database", database },
                    { "SourceSettings:Container", container },
                    { "SinkSettings:ConnectionString", connectionString },
                    { "SinkSettings:Database", database },
                    { "SinkSettings:Container", container },
                    { "SinkSettings:RecreateContainer", "false" },
                })
                .Build();
            
            var loader = new Mock<IExtensionLoader>();
            var sourceExtension = new Mock<IDataSourceExtension>();
            sourceExtension.SetupGet(ds => ds.DisplayName).Returns(cosmosExtension);
            loader
                .Setup(l => l.LoadExtensions<IDataSourceExtension>(It.IsAny<CompositionContainer>()))
                .Returns(new List<IDataSourceExtension> { sourceExtension.Object });

            var sinkExtension = new Mock<IDataSinkExtension>();
            sinkExtension.SetupGet(ds => ds.DisplayName).Returns(cosmosExtension);
            loader
                .Setup(l => l.LoadExtensions<IDataSinkExtension>(It.IsAny<CompositionContainer>()))
                .Returns(new List<IDataSinkExtension> { sinkExtension.Object });
            
            var handler = new RunCommand.CommandHandler(loader.Object,
                configuration,
                NullLoggerFactory.Instance);

            var parseResult = new RootCommand().Parse(Array.Empty<string>());
            var result = handler.Invoke(new InvocationContext(parseResult));
            
            // Should succeed when RecreateContainer is false even with same container
            Assert.AreEqual(0, result);
            sourceExtension.Verify(se => se.ReadAsync(It.IsAny<IConfiguration>(), It.IsAny<ILogger>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public void Invoke_WithSameCosmosSourceAndSinkDifferentDatabase_Succeeds()
        {
            const string cosmosExtension = "Cosmos-nosql";
            const string connectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=test";
            const string sourceDatabase = "sourceDb";
            const string sinkDatabase = "sinkDb";
            const string container = "testContainer";
            
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "Source", cosmosExtension },
                    { "Sink", cosmosExtension },
                    { "SourceSettings:ConnectionString", connectionString },
                    { "SourceSettings:Database", sourceDatabase },
                    { "SourceSettings:Container", container },
                    { "SinkSettings:ConnectionString", connectionString },
                    { "SinkSettings:Database", sinkDatabase },
                    { "SinkSettings:Container", container },
                    { "SinkSettings:RecreateContainer", "true" },
                })
                .Build();
            
            var loader = new Mock<IExtensionLoader>();
            var sourceExtension = new Mock<IDataSourceExtension>();
            sourceExtension.SetupGet(ds => ds.DisplayName).Returns(cosmosExtension);
            loader
                .Setup(l => l.LoadExtensions<IDataSourceExtension>(It.IsAny<CompositionContainer>()))
                .Returns(new List<IDataSourceExtension> { sourceExtension.Object });

            var sinkExtension = new Mock<IDataSinkExtension>();
            sinkExtension.SetupGet(ds => ds.DisplayName).Returns(cosmosExtension);
            loader
                .Setup(l => l.LoadExtensions<IDataSinkExtension>(It.IsAny<CompositionContainer>()))
                .Returns(new List<IDataSinkExtension> { sinkExtension.Object });
            
            var handler = new RunCommand.CommandHandler(loader.Object,
                configuration,
                NullLoggerFactory.Instance);

            var parseResult = new RootCommand().Parse(Array.Empty<string>());
            var result = handler.Invoke(new InvocationContext(parseResult));
            
            // Should succeed when database is different
            Assert.AreEqual(0, result);
            sourceExtension.Verify(se => se.ReadAsync(It.IsAny<IConfiguration>(), It.IsAny<ILogger>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public void Invoke_WithSameCosmosSourceAndSinkDifferentContainer_Succeeds()
        {
            const string cosmosExtension = "Cosmos-nosql";
            const string connectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=test";
            const string database = "testDb";
            const string sourceContainer = "sourceContainer";
            const string sinkContainer = "sinkContainer";
            
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "Source", cosmosExtension },
                    { "Sink", cosmosExtension },
                    { "SourceSettings:ConnectionString", connectionString },
                    { "SourceSettings:Database", database },
                    { "SourceSettings:Container", sourceContainer },
                    { "SinkSettings:ConnectionString", connectionString },
                    { "SinkSettings:Database", database },
                    { "SinkSettings:Container", sinkContainer },
                    { "SinkSettings:RecreateContainer", "true" },
                })
                .Build();
            
            var loader = new Mock<IExtensionLoader>();
            var sourceExtension = new Mock<IDataSourceExtension>();
            sourceExtension.SetupGet(ds => ds.DisplayName).Returns(cosmosExtension);
            loader
                .Setup(l => l.LoadExtensions<IDataSourceExtension>(It.IsAny<CompositionContainer>()))
                .Returns(new List<IDataSourceExtension> { sourceExtension.Object });

            var sinkExtension = new Mock<IDataSinkExtension>();
            sinkExtension.SetupGet(ds => ds.DisplayName).Returns(cosmosExtension);
            loader
                .Setup(l => l.LoadExtensions<IDataSinkExtension>(It.IsAny<CompositionContainer>()))
                .Returns(new List<IDataSinkExtension> { sinkExtension.Object });
            
            var handler = new RunCommand.CommandHandler(loader.Object,
                configuration,
                NullLoggerFactory.Instance);

            var parseResult = new RootCommand().Parse(Array.Empty<string>());
            var result = handler.Invoke(new InvocationContext(parseResult));
            
            // Should succeed when container is different
            Assert.AreEqual(0, result);
            sourceExtension.Verify(se => se.ReadAsync(It.IsAny<IConfiguration>(), It.IsAny<ILogger>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public void Invoke_WithDifferentExtensionTypesAndRecreateContainer_Succeeds()
        {
            const string sourceExtension = "Json";
            const string sinkExtension = "Cosmos-nosql";
            
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "Source", sourceExtension },
                    { "Sink", sinkExtension },
                    { "SourceSettings:FilePath", "test.json" },
                    { "SinkSettings:ConnectionString", "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=test" },
                    { "SinkSettings:Database", "testDb" },
                    { "SinkSettings:Container", "testContainer" },
                    { "SinkSettings:RecreateContainer", "true" },
                })
                .Build();
            
            var loader = new Mock<IExtensionLoader>();
            var source = new Mock<IDataSourceExtension>();
            source.SetupGet(ds => ds.DisplayName).Returns(sourceExtension);
            loader
                .Setup(l => l.LoadExtensions<IDataSourceExtension>(It.IsAny<CompositionContainer>()))
                .Returns(new List<IDataSourceExtension> { source.Object });

            var sink = new Mock<IDataSinkExtension>();
            sink.SetupGet(ds => ds.DisplayName).Returns(sinkExtension);
            loader
                .Setup(l => l.LoadExtensions<IDataSinkExtension>(It.IsAny<CompositionContainer>()))
                .Returns(new List<IDataSinkExtension> { sink.Object });
            
            var handler = new RunCommand.CommandHandler(loader.Object,
                configuration,
                NullLoggerFactory.Instance);

            var parseResult = new RootCommand().Parse(Array.Empty<string>());
            var result = handler.Invoke(new InvocationContext(parseResult));
            
            // Should succeed when source and sink are different extension types
            Assert.AreEqual(0, result);
            source.Verify(se => se.ReadAsync(It.IsAny<IConfiguration>(), It.IsAny<ILogger>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public void Invoke_WithSameCosmosSourceAndSinkUsingAccountEndpoint_ThrowsException()
        {
            const string cosmosExtension = "Cosmos-nosql";
            const string accountEndpoint = "https://test.documents.azure.com:443/";
            const string database = "testDb";
            const string container = "testContainer";
            
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "Source", cosmosExtension },
                    { "Sink", cosmosExtension },
                    { "SourceSettings:AccountEndpoint", accountEndpoint },
                    { "SourceSettings:Database", database },
                    { "SourceSettings:Container", container },
                    { "SinkSettings:AccountEndpoint", accountEndpoint },
                    { "SinkSettings:Database", database },
                    { "SinkSettings:Container", container },
                    { "SinkSettings:RecreateContainer", "true" },
                })
                .Build();
            
            var loader = new Mock<IExtensionLoader>();
            var sourceExtension = new Mock<IDataSourceExtension>();
            sourceExtension.SetupGet(ds => ds.DisplayName).Returns(cosmosExtension);
            loader
                .Setup(l => l.LoadExtensions<IDataSourceExtension>(It.IsAny<CompositionContainer>()))
                .Returns(new List<IDataSourceExtension> { sourceExtension.Object });

            var sinkExtension = new Mock<IDataSinkExtension>();
            sinkExtension.SetupGet(ds => ds.DisplayName).Returns(cosmosExtension);
            loader
                .Setup(l => l.LoadExtensions<IDataSinkExtension>(It.IsAny<CompositionContainer>()))
                .Returns(new List<IDataSinkExtension> { sinkExtension.Object });
            
            var handler = new RunCommand.CommandHandler(loader.Object,
                configuration,
                NullLoggerFactory.Instance);

            var parseResult = new RootCommand().Parse(Array.Empty<string>());
            
            // Should throw exception when same container is used with AccountEndpoint
            var exception = Assert.ThrowsException<InvalidOperationException>(() => handler.Invoke(new InvocationContext(parseResult)));
            Assert.IsTrue(exception.Message.Contains("same Cosmos DB container"));
        }
    }
}