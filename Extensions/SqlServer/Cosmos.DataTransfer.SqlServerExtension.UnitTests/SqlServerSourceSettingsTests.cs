using Cosmos.DataTransfer.Interfaces;
using System.ComponentModel.DataAnnotations;
using Cosmos.DataTransfer.Common.UnitTests;
using Microsoft.Data.Sqlite;
using System.Data;

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
        var fn = Path.GetTempFileName();
        var settings = new SqlServerSourceSettings {
            QueryText = "SELECT 1;",
            FilePath = fn
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
    public void TestSourceSettings_Validation_FileNotFound()
    {
        var fn = Path.GetTempFileName();
        var settings = new SqlServerSourceSettings {
            ConnectionString = "Connection, please",
            FilePath = "dmt.sql"
        };

        var validationResults = settings.Validate(new ValidationContext(settings)).ToList();
        Assert.AreEqual(1, validationResults.Count);
        Assert.IsTrue(validationResults[0].ErrorMessage!.StartsWith("Could not read `FilePath`. Reason:"));
        CollectionAssert.AreEqual(new string[] { "FilePath" }, validationResults[0].MemberNames.ToArray());
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
            FilePath = filePath == "filename" ? Path.GetTempFileName() : filePath
        };

        var validationResults = settings.Validate(new ValidationContext(settings));
        Assert.AreEqual(0, validationResults.Count());
        settings.Validate();
    }

    [TestMethod]
    public void TestSourceSettings_GetQueryText1() {
        var settings = new SqlServerSourceSettings() {
            QueryText = "SELECT 1"
        };
        Assert.AreEqual("SELECT 1", settings.GetQueryText());

        var fn = Path.GetTempFileName();
        settings.FilePath = fn; // But this shouldn't occur, as the settings are invalid.
        File.WriteAllText(fn, "More SQL");
        Assert.AreEqual("More SQL", settings.GetQueryText());

        settings.QueryText = "";
        Assert.AreEqual("More SQL", settings.GetQueryText());
    }

    [TestMethod]
    public void TestSourceSettings_Parameters() {
        var settings = new SqlServerSourceSettings();

        Assert.AreEqual(0, settings.GetDbParameters(SqliteFactory.Instance).Count());
        settings.Parameters = new Dictionary<string, object> {
                { "str", "str" },
                { "bool", true },
                { "int", 100 },
                { "long", 100L },
                { "double", 3.14d },
                { "float", 2.718f },
                { "datetime", DateTime.UtcNow }
            };
        
        var parameters = settings.GetDbParameters(SqliteFactory.Instance);
        int i = -1;
        Assert.AreEqual("str", parameters[++i].ParameterName);
        Assert.AreEqual("str", parameters[i].Value);
        Assert.AreEqual(DbType.String, parameters[i].DbType);
        Assert.AreEqual("bool", parameters[++i].ParameterName);
        Assert.AreEqual(true, parameters[i].Value);
        Assert.AreEqual(DbType.Boolean, parameters[i].DbType);
        Assert.AreEqual("int", parameters[++i].ParameterName);
        Assert.AreEqual(100, parameters[i].Value);
        Assert.AreEqual(DbType.Int32, parameters[i].DbType);
        Assert.AreEqual("long", parameters[++i].ParameterName);
        Assert.AreEqual(100L, parameters[i].Value);
        Assert.AreEqual(DbType.Int64, parameters[i].DbType);
        Assert.AreEqual("double", parameters[++i].ParameterName);
        Assert.AreEqual(3.14, parameters[i].Value);
        Assert.AreEqual(DbType.Double, parameters[i].DbType);
        Assert.AreEqual("float", parameters[++i].ParameterName);
        Assert.AreEqual(2.718f, parameters[i].Value);
        Assert.AreEqual(DbType.Single, parameters[i].DbType);
        Assert.AreEqual("datetime", parameters[++i].ParameterName);
        //Assert.AreEqual(2.718f, parameters[i].Value);
        Assert.AreEqual(DbType.String, parameters[i].DbType);
    }
}
