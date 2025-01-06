using Cosmos.DataTransfer.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace Cosmos.DataTransfer.SqlServerExtension.UnitTests;

[TestClass]
public class SqlServerSourceSettingsTests 
{

    [TestMethod]
    public void TestSourceSettings_ValidationFails1()
    {
        var settings = new SqlServerSourceSettings {
        };

        var validationResults = settings.Validate(new ValidationContext(settings)).ToList();
        var expected = new List<ValidationResult>() {
            new ValidationResult("The `ConnectionString` field is required.",
            new string[] { "ConnectionString" }),
            new ValidationResult("Either `QueryText` or `FilePath` are required!",
                new string[] { "QueryText", "FilePath"})
        };
        CollectionAssert.AreEquivalent(expected.Select(x => x.ErrorMessage).ToList(),
            validationResults.Select(x => x.ErrorMessage).ToList());
                CollectionAssert.AreEquivalent(expected.SelectMany(x => x.MemberNames).ToList(),
            validationResults.SelectMany(x => x.MemberNames).ToList());

        Assert.ThrowsException<AggregateException>(() => settings.Validate());
    }

    [TestMethod]
    public void TestSourceSettings_ValidationFails2()
    {
        var settings = new SqlServerSourceSettings {
            QueryText = "SELECT 1;",
            FilePath = "dmt-query.sql"
        };

        var validationResults = settings.Validate(new ValidationContext(settings)).ToList();
        var expected = new List<ValidationResult>() {
            new ValidationResult("The `ConnectionString` field is required.",
            new string[] { "ConnectionString" }),
            new ValidationResult("Both `QueryText` and `FilePath` are not allowed.",
                new string[] { "QueryText", "FilePath"})
        };
        CollectionAssert.AreEquivalent(expected.Select(x => x.ErrorMessage).ToList(),
            validationResults.Select(x => x.ErrorMessage).ToList());
                CollectionAssert.AreEquivalent(expected.SelectMany(x => x.MemberNames).ToList(),
            validationResults.SelectMany(x => x.MemberNames).ToList());

        Assert.ThrowsException<AggregateException>(() => settings.Validate());
    }

    [TestMethod]
    [DataRow("SELECT 1", null)]
    [DataRow("SELECT 1", " ")]
    [DataRow(null, "filename")]
    [DataRow("  ", "filename")]
    public void TestSourceSettings_ValidationSuccess(string queryText, string filePath) {
        var settings = new SqlServerSourceSettings {
            ConnectionString = "Server",
            QueryText = queryText,
            FilePath = filePath
        };

        var validationResults = settings.Validate(new ValidationContext(settings));
        Assert.AreEqual(0, validationResults.Count());

        settings.Validate();
    }
}
