using System.Globalization;
using Cosmos.DataTransfer.Common.UnitTests;
using Microsoft.Extensions.Logging.Abstractions;

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

        var originalText = await File.ReadAllLinesAsync(fileIn);
        var finalText = await File.ReadAllLinesAsync(fileOut);
        for (var index = 0; index < originalText.Length; index++)
        {
            var a = originalText.ElementAtOrDefault(index);
            var b = finalText.ElementAtOrDefault(index);
            Assert.AreEqual(a, b, $"Different text at row {index}");
        }

        CollectionAssert.AreEquivalent(originalText, finalText);
    }

}
