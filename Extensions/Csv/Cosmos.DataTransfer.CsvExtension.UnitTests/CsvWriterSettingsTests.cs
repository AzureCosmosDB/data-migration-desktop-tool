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
    public void TestDefault() {
        var settings = new CsvWriterSettings() { };

        Assert.AreEqual(CultureInfo.InvariantCulture, settings.GetCultureInfo());        
        Assert.AreEqual(0, settings.Validate(new ValidationContext(this)).Count());
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
        Assert.AreEqual(CultureInfo.InvariantCulture, settings.GetCultureInfo());        
        Assert.AreEqual(0, settings.Validate(new ValidationContext(this)).Count());
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
        Assert.AreEqual(CultureInfo.CurrentCulture, settings.GetCultureInfo());
        Assert.AreEqual(0, settings.Validate(new ValidationContext(this)).Count());
    }

    [TestMethod]
    public void TestCurrentCultureByName() {
        var settings = new CsvWriterSettings() {
            Culture = CultureInfo.CurrentCulture.Name
        };
        Assert.AreEqual(CultureInfo.CurrentCulture, settings.GetCultureInfo());
        Assert.AreEqual(0, settings.Validate(new ValidationContext(this)).Count());
    }

    [TestMethod]
    public void TestCultureFails() {
        var settings = new CsvWriterSettings() {
            Culture = "not a culture"
        };
        var results = settings.Validate(new ValidationContext(this)).ToArray();
        Assert.AreEqual(1, results.Count());
        Assert.AreEqual("Could not find CultureInfo `not a culture` on this system.", results.First().ErrorMessage);
    }

    [TestMethod]
    public void TestCultureMissing() {
        var settings = new CsvWriterSettings() {
            Culture = ""
        };
        var results = settings.Validate(new ValidationContext(this)).ToArray();
        Assert.AreEqual(1, results.Count());
        Assert.AreEqual("Culture missing.", results.First().ErrorMessage);
    }

    [TestMethod]
    public void TestCultureNull()
    {
        var settings = new CsvWriterSettings()
        {
            Culture = null
        };
        var results = settings.Validate(new ValidationContext(this)).ToArray();
        Assert.AreEqual(1, results.Count());
        Assert.AreEqual("Culture missing.", results.First().ErrorMessage);
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
        Assert.AreEqual("1,2", result);
    }
}
