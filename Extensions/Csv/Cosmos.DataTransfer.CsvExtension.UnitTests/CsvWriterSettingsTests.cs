using System.Globalization;
using Cosmos.DataTransfer.JsonExtension.UnitTests;
using Microsoft.Extensions.Logging.Abstractions;
using System.ComponentModel.DataAnnotations;
using Cosmos.DataTransfer.CsvExtension.Settings;
using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.JsonExtension;

namespace Cosmos.DataTransfer.CsvExtension.UnitTests;

[TestClass]
public class CsvWriterSettingsTests
{


    [TestMethod]
    public void TestDefault(string culture) {
        var settings = new CsvWriterSettings() { };

        Assert.AreEqual(settings.GetCultureInfo(), CultureInfo.InvariantCulture);        
        Assert.AreEqual(settings.Validate(new ValidationContext(this)).Count(), 0);
    }
    
    [TestMethod]
    [DataRow("invariant")]
    [DataRow("Invariant")]
    [DataRow("invariantCulture")]
    [DataRow("invariantculture")]
    public void TestInvariantCulture(string culture) {
        var settings = new CsvWriterSettings() {
            Culture = culture
        };
        Assert.AreEqual(settings.GetCultureInfo(), CultureInfo.InvariantCulture);        
        Assert.AreEqual(settings.Validate(new ValidationContext(this)).Count(), 0);
    }

    [TestMethod]
    [DataRow("current")]
    [DataRow("Current")]
    [DataRow("currentCultuRE")]
    [DataRow("currentCulture")]
    public void TestCurrentCulture(string culture) {
        var settings = new CsvWriterSettings() {
            Culture = culture
        };
        Assert.AreEqual(settings.GetCultureInfo(), CultureInfo.CurrentCulture);
        Assert.AreEqual(settings.Validate(new ValidationContext(this)).Count(), 0);
    }

    [TestMethod]
    public void TestCurrentCultureByName() {
        var settings = new CsvWriterSettings() {
            Culture = CultureInfo.CurrentCulture.Name
        };
        Assert.AreEqual(settings.GetCultureInfo(), CultureInfo.CurrentCulture);
        Assert.AreEqual(settings.Validate(new ValidationContext(this)).Count(), 0);
    }

    [TestMethod]
    public void TestCultureFails() {
        var settings = new CsvWriterSettings() {
            Culture = "not a culture"
        };
        var results = settings.Validate(new ValidationContext(this));
        Assert.AreEqual(results.Count(), 1);
        Assert.AreEqual(results.First().ErrorMessage, "Could not find CultureInfo `not a culture` on this system.");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void TestCultureMissing(string culture) {
        var settings = new CsvWriterSettings() {
            Culture = culture
        };
        var results = settings.Validate(new ValidationContext(this));
        Assert.AreEqual(results.Count(), 1);
        Assert.AreEqual(results.First().ErrorMessage, "Culture missing.");
    }

    [TestMethod]
    public async Task TestDanishCulture() {
        var outputFile = Path.GetTempFileName();
        var config = TestHelpers.CreateConfig(new Dictionary<string, string>
            {
                { "FilePath", outputFile },
                { "IncludeHeader", "false" },
                { "Culture", "da-DK" },
                { "Delimiter", ";" }
            });

        var data = new List<DictionaryDataItem>
        {
            new(new Dictionary<string, object?>
            {
                { "Value", 1.2 }
            })
        };

        var sink = new CsvFileSink();

        await sink.WriteAsync(data.ToAsyncEnumerable(), config, new JsonFileSource(), NullLogger.Instance);
        var result = await File.ReadAllTextAsync(outputFile);
        Assert.AreEqual(result, "1,2");
    }
}
