using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Cosmos.DataTransfer.JsonExtension;
using Cosmos.DataTransfer.JsonExtension.UnitTests;
using System.Diagnostics; // For TestHelpers


namespace Cosmos.DataTransfer.Common.UnitTest;

[TestClass]
public class FileSinkDataSinkTests {
    [TestMethod]
    public async Task TestFileSinkDataSinkGzip() {
        var tempfile = Path.GetTempFileName() + ".gz";
        var sink = new FileDataSink();
        var dataformatter = new JsonFileSource(); // In lieu of a mock
        var config = TestHelpers.CreateConfig(new Dictionary<string,string>() {
            {"FilePath", tempfile},
            {"Gzip", "true"}
        });


        Func<Stream, Task> writeToStream = async (stream) => {
            await stream.WriteAsync(Encoding.ASCII.GetBytes("Hello world!"));
        };

        await sink.WriteToTargetAsync(writeToStream, config, dataformatter, NullLogger.Instance);
        
        using FileStream compressedFileStream = File.Open(tempfile, FileMode.Open);
        using var decompressor = new GZipStream(compressedFileStream, CompressionMode.Decompress);
        var bytes = new byte[100];
        await decompressor.ReadAsync(bytes);
        Assert.AreEqual("Hello world!", 
            Encoding.ASCII.GetString(bytes, 0, bytes.Length).TrimEnd('\0'));
    }
}
