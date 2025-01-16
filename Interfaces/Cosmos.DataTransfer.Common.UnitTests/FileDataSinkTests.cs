using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Cosmos.DataTransfer.JsonExtension;
using Cosmos.DataTransfer.JsonExtension.UnitTests;
using Cosmos.DataTransfer.Interfaces;
using Moq;

namespace Cosmos.DataTransfer.Common.UnitTest;

[TestClass]
public class FileSinkDataSinkTests {

    public static IEnumerable<object[]> Test_WriteToTargetAsyncData { get {

        yield return new object[] { CompressionEnum.None, "", "" };
        yield return new object[] { CompressionEnum.None, ".txt", ".txt" };
        yield return new object[] { CompressionEnum.Brotli, "", ".br" };
        yield return new object[] { CompressionEnum.Brotli, ".br", ".br" };
        yield return new object[] { CompressionEnum.Gzip, "", ".gz" };
        yield return new object[] { CompressionEnum.Gzip, ".gz", ".gz" };
        yield return new object[] { CompressionEnum.Gzip, ".gzip", ".gzip" };
        yield return new object[] { CompressionEnum.Deflate, "", ".zz" };
        yield return new object[] { CompressionEnum.Deflate, ".zz", ".zz" };
    } }

    [TestMethod]
    [DoNotParallelize]
    [DynamicData(nameof(Test_WriteToTargetAsyncData))]
    public async Task Test_WriteToTargetAsync(CompressionEnum compression, string suffix, string expected_ext) {
        var source = new Mock<IDataSourceExtension>();
        FileDataSink sink = new();
        var filePath = Path.GetTempFileName();
        var config = TestHelpers.CreateConfig(new Dictionary<string,string>() {
            { "FilePath", filePath + suffix },
            { "Compression", compression.ToString() }
        });

        var str = Encoding.UTF8.GetBytes("Hello world!");
        await sink.WriteToTargetAsync(writer => writer.WriteAsync(str).AsTask(),
            config, source.Object, NullLogger.Instance);
        
        var dataSource = new FileDataSource();
        var stream = dataSource.ReadFile(filePath + expected_ext, CompressionEnum.None, NullLogger.Instance)!;
        var buffer = new byte[100];
        await stream.ReadAsync(buffer.AsMemory(0, buffer.Length));
        var result = Encoding.UTF8.GetString(buffer);
        Assert.AreEqual("Hello world!", result.TrimEnd('\0'), $"compression: {compression}, suffix: {suffix}, expected extension: {expected_ext}.");
    }

    [TestMethod]
    public void Test_GetSettings() {
        var sink = new FileDataSink();
        var response = sink.GetSettings().ToArray();
        Assert.AreEqual(1, response.Length);
        Assert.IsInstanceOfType(response[0], typeof(FileSinkSettings));
    }

}
