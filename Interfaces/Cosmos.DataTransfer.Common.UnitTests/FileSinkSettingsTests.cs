using Microsoft.VisualStudio.TestTools.UnitTesting;
using Cosmos.DataTransfer.Interfaces;

namespace Cosmos.DataTransfer.Common.UnitTests;

[TestClass]
public class FileSinkSettingsTests {

    [TestMethod]
    public void TestValidate_PassMinimum() {
        var settings = new FileSinkSettings() {
            FilePath = "A file",
        };
        settings.Validate();
    }

    [TestMethod]
    public void TestValidate_Fails() {
        var settings = new FileSinkSettings() {
            FilePath = " ",
        };
        var e = Assert.ThrowsException<AggregateException>(() => settings.Validate());
        Assert.AreEqual(e.InnerException!.Message, "The FilePath field is required.");
    }

    [TestMethod]
    [DataRow(false, "None", true)]
    [DataRow(false, "Gzip", true)]
    [DataRow(false, "Brotli", true)]
    [DataRow(false, "Deflate", true)]
    [DataRow(true, "None", true)]
    [DataRow(true, "Gzip", false)]
    [DataRow(true, "Brotli", false)]
    [DataRow(true, "Deflate", false)]
    public void TestValidate_TestCombinations(bool append, string compression, bool pass) {
        var settings = new FileSinkSettings() {
            FilePath = "A file",
            Append = append,
            Compression = Enum.Parse<CompressionEnum>(compression),
        };
        if (!pass) {
            Assert.ThrowsException<AggregateException>(() => settings.Validate());
        } else {
            settings.Validate();
        }
    }


}