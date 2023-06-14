using Cosmos.DataTransfer.JsonExtension;
using Cosmos.DataTransfer.JsonExtension.UnitTests;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;

namespace Cosmos.DataTransfer.CsvExtension.UnitTests;

[TestClass]
public class CsvFileRoundTripTests
{
    [TestMethod]
    public async Task WriteAsync_fromReadAsync_ProducesIdenticalFile()
    {
        var input = new CsvFileSource();
        var output = new CsvFileSink();

        const string fileIn = "Data/SimpleData.csv";
        const string fileOut = $"{nameof(WriteAsync_fromReadAsync_ProducesIdenticalFile)}_out.csv";

        var sourceConfig = TestHelpers.CreateConfig(new Dictionary<string, string>
        {
            { "FilePath", fileIn },
        });
        var sinkConfig = TestHelpers.CreateConfig(new Dictionary<string, string>
        {
            { "FilePath", fileOut },
        });

        await output.WriteAsync(input.ReadAsync(sourceConfig, NullLogger.Instance), sinkConfig, input, NullLogger.Instance);

        Assert.AreEqual(await File.ReadAllTextAsync(fileIn), await File.ReadAllTextAsync(fileOut));
    }

}
