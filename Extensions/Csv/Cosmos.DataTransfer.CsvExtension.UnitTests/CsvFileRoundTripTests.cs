using System.Globalization;
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

        string originalText = await File.ReadAllTextAsync(fileIn);
        string finalText = await File.ReadAllTextAsync(fileOut);
        for (var index = 0; index < originalText.Length; index++)
        {
            var a = originalText[index];
            var b = finalText[index];
            Assert.AreEqual(a, b, $"Different character at position {index}");
        }

        Assert.AreEqual(originalText, finalText, false, CultureInfo.InvariantCulture);
    }

}
