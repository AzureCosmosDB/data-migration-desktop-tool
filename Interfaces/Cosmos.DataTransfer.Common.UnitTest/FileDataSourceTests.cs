using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Cosmos.DataTransfer.JsonExtension;
using Cosmos.DataTransfer.JsonExtension.UnitTests;
using System.Diagnostics; // For TestHelpers


namespace Cosmos.DataTransfer.Common.UnitTest;

[TestClass]
public class FileSinkDataSourceTests {
    [TestMethod]
    public async Task TestFileSinkDataSourceGzip() {
        var fileSource = new FileDataSource();
        var config = TestHelpers.CreateConfig(new Dictionary<string,string>() {
            {"FilePath", "Data/foobar.gz"}
        });

        var streams = fileSource.ReadSourceAsync(config, NullLogger.Instance);
        var i = 0;
        await foreach (var stream in streams.WithCancellation(new CancellationToken()))
        {
            i++;
            if (stream != null) {
                var bytes = new byte[100];
                await stream.ReadAsync(bytes);
                Assert.AreEqual("foobar", 
                    Encoding.ASCII.GetString(bytes, 0, bytes.Length).TrimEnd('\0'));
            }
        }
        Assert.AreEqual(1, i);
    }
}
