using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging.Abstractions;
using Cosmos.DataTransfer.JsonExtension.UnitTests;

namespace Cosmos.DataTransfer.Common.UnitTests;

[TestClass]
public class FileSinkDataSourceTests {

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
            .SelectAwait(async stream => {
                var buffer = new byte[100];
                await stream!.ReadAsync(buffer.AsMemory(0, buffer.Length));
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