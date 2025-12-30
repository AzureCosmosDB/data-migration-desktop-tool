using System.ComponentModel.DataAnnotations;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Cosmos.DataTransfer.SqlServerExtension.UnitTests;

[TestClass]
public class SqlServerSinkSettingsTests
{
    [TestMethod]
    public void TestSinkSettings_DefaultWriteMode_IsInsert()
    {
        var settings = new SqlServerSinkSettings
        {
            ConnectionString = "Server=.;Database=Test;",
            TableName = "TestTable",
            ColumnMappings = new List<ColumnMapping>
            {
                new ColumnMapping { ColumnName = "Id" }
            }
        };

        Assert.AreEqual(SqlWriteMode.Insert, settings.WriteMode, "WriteMode should default to Insert");
    }

    [TestMethod]
    public void TestSinkSettings_WriteMode_CanBeSetToUpsert()
    {
        var settings = new SqlServerSinkSettings
        {
            ConnectionString = "Server=.;Database=Test;",
            TableName = "TestTable",
            WriteMode = SqlWriteMode.Upsert,
            PrimaryKeyColumns = new List<string> { "Id" },
            ColumnMappings = new List<ColumnMapping>
            {
                new ColumnMapping { ColumnName = "Id" }
            }
        };

        Assert.AreEqual(SqlWriteMode.Upsert, settings.WriteMode, "WriteMode should be settable to Upsert");
    }

    [TestMethod]
    public void TestSinkSettings_UpsertMode_RequiresPrimaryKeyColumns()
    {
        var settings = new SqlServerSinkSettings
        {
            ConnectionString = "Server=.;Database=Test;",
            TableName = "TestTable",
            WriteMode = SqlWriteMode.Upsert,
            ColumnMappings = new List<ColumnMapping>
            {
                new ColumnMapping { ColumnName = "Id" }
            }
        };

        var validationResults = settings.Validate(new ValidationContext(settings)).ToList();
        
        Assert.IsTrue(validationResults.Any(v => v.MemberNames.Contains(nameof(SqlServerSinkSettings.PrimaryKeyColumns))),
            "Validation should fail when PrimaryKeyColumns is empty and WriteMode is Upsert");
        
        Assert.IsTrue(validationResults.Any(v => v.ErrorMessage!.Contains("PrimaryKeyColumns must be specified")),
            "Validation error should mention PrimaryKeyColumns requirement");
    }

    [TestMethod]
    public void TestSinkSettings_UpsertMode_WithPrimaryKeyColumns_PassesValidation()
    {
        var settings = new SqlServerSinkSettings
        {
            ConnectionString = "Server=.;Database=Test;",
            TableName = "TestTable",
            WriteMode = SqlWriteMode.Upsert,
            PrimaryKeyColumns = new List<string> { "Id" },
            ColumnMappings = new List<ColumnMapping>
            {
                new ColumnMapping { ColumnName = "Id" },
                new ColumnMapping { ColumnName = "Name" }
            }
        };

        var validationResults = settings.Validate(new ValidationContext(settings)).ToList();
        
        Assert.IsFalse(validationResults.Any(v => v.MemberNames.Contains(nameof(SqlServerSinkSettings.PrimaryKeyColumns))),
            "Validation should pass when PrimaryKeyColumns is provided with Upsert mode");
    }

    [TestMethod]
    public void TestSinkSettings_InsertMode_DoesNotRequirePrimaryKeyColumns()
    {
        var settings = new SqlServerSinkSettings
        {
            ConnectionString = "Server=.;Database=Test;",
            TableName = "TestTable",
            WriteMode = SqlWriteMode.Insert,
            ColumnMappings = new List<ColumnMapping>
            {
                new ColumnMapping { ColumnName = "Id" }
            }
        };

        var validationResults = settings.Validate(new ValidationContext(settings)).ToList();
        
        Assert.IsFalse(validationResults.Any(v => v.MemberNames.Contains(nameof(SqlServerSinkSettings.PrimaryKeyColumns))),
            "Validation should not require PrimaryKeyColumns when WriteMode is Insert");
    }

    [TestMethod]
    public void TestSinkSettings_WriteMode_DeserializesFromJson()
    {
        // Test JSON to enum conversion for Insert
        var jsonInsert = """{"WriteMode": "Insert"}""";
        var configInsert = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonInsert)))
            .Build();
        var settingsInsert = configInsert.Get<SqlServerSinkSettings>();
        Assert.AreEqual(SqlWriteMode.Insert, settingsInsert?.WriteMode, "WriteMode should be deserialized from JSON string 'Insert'");

        // Test JSON to enum conversion for Upsert
        var jsonUpsert = """{"WriteMode": "Upsert"}""";
        var configUpsert = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonUpsert)))
            .Build();
        var settingsUpsert = configUpsert.Get<SqlServerSinkSettings>();
        Assert.AreEqual(SqlWriteMode.Upsert, settingsUpsert?.WriteMode, "WriteMode should be deserialized from JSON string 'Upsert'");
    }

    [TestMethod]
    public void TestSinkSettings_PrimaryKeyColumns_DeserializesFromJson()
    {
        var json = """
        {
            "ConnectionString": "Server=.;Database=Test;",
            "TableName": "TestTable",
            "WriteMode": "Upsert",
            "PrimaryKeyColumns": ["Id", "TenantId"],
            "ColumnMappings": [
                {"ColumnName": "Id"},
                {"ColumnName": "TenantId"}
            ]
        }
        """;
        
        var config = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
            .Build();
        var settings = config.Get<SqlServerSinkSettings>();
        
        Assert.IsNotNull(settings, "Settings should be deserialized");
        Assert.AreEqual(SqlWriteMode.Upsert, settings!.WriteMode, "WriteMode should be Upsert");
        Assert.IsNotNull(settings.PrimaryKeyColumns, "PrimaryKeyColumns should not be null");
        Assert.AreEqual(2, settings.PrimaryKeyColumns.Count, "Should have 2 primary key columns");
        CollectionAssert.AreEqual(new[] { "Id", "TenantId" }, settings.PrimaryKeyColumns, "Primary key columns should match");
    }

    [TestMethod]
    public void TestSinkSettings_CompositePrimaryKey_PassesValidation()
    {
        var settings = new SqlServerSinkSettings
        {
            ConnectionString = "Server=.;Database=Test;",
            TableName = "TestTable",
            WriteMode = SqlWriteMode.Upsert,
            PrimaryKeyColumns = new List<string> { "TenantId", "UserId" },
            ColumnMappings = new List<ColumnMapping>
            {
                new ColumnMapping { ColumnName = "TenantId" },
                new ColumnMapping { ColumnName = "UserId" },
                new ColumnMapping { ColumnName = "Name" }
            }
        };

        var validationResults = settings.Validate(new ValidationContext(settings)).ToList();
        
        Assert.IsFalse(validationResults.Any(v => v.MemberNames.Contains(nameof(SqlServerSinkSettings.PrimaryKeyColumns))),
            "Validation should pass with composite primary key");
    }

    [TestMethod]
    public void TestSinkSettings_AllColumnsArePrimaryKeys_FailsValidation()
    {
        var settings = new SqlServerSinkSettings
        {
            ConnectionString = "Server=.;Database=Test;",
            TableName = "TestTable",
            WriteMode = SqlWriteMode.Upsert,
            PrimaryKeyColumns = new List<string> { "Id", "Name" },
            ColumnMappings = new List<ColumnMapping>
            {
                new ColumnMapping { ColumnName = "Id" },
                new ColumnMapping { ColumnName = "Name" }
            }
        };

        var validationResults = settings.Validate(new ValidationContext(settings)).ToList();
        
        Assert.IsTrue(validationResults.Any(v => v.MemberNames.Contains(nameof(SqlServerSinkSettings.ColumnMappings))),
            "Validation should fail when all columns are primary keys without DeleteNotMatchedBySource");
        
        Assert.IsTrue(validationResults.Any(v => v.ErrorMessage!.Contains("non-primary key column")),
            "Validation error should mention non-primary key column requirement");
    }

    [TestMethod]
    public void TestSinkSettings_AllColumnsArePrimaryKeys_WithDelete_PassesValidation()
    {
        var settings = new SqlServerSinkSettings
        {
            ConnectionString = "Server=.;Database=Test;",
            TableName = "TestTable",
            WriteMode = SqlWriteMode.Upsert,
            PrimaryKeyColumns = new List<string> { "Id", "Name" },
            DeleteNotMatchedBySource = true,
            ColumnMappings = new List<ColumnMapping>
            {
                new ColumnMapping { ColumnName = "Id" },
                new ColumnMapping { ColumnName = "Name" }
            }
        };

        var validationResults = settings.Validate(new ValidationContext(settings)).ToList();
        
        Assert.IsFalse(validationResults.Any(v => v.MemberNames.Contains(nameof(SqlServerSinkSettings.ColumnMappings))),
            "Validation should pass when all columns are primary keys but DeleteNotMatchedBySource is true");
    }

    [TestMethod]
    public void TestSinkSettings_DeleteNotMatchedBySource_DefaultsToFalse()
    {
        var settings = new SqlServerSinkSettings
        {
            ConnectionString = "Server=.;Database=Test;",
            TableName = "TestTable",
            WriteMode = SqlWriteMode.Upsert,
            PrimaryKeyColumns = new List<string> { "Id" },
            ColumnMappings = new List<ColumnMapping>
            {
                new ColumnMapping { ColumnName = "Id" },
                new ColumnMapping { ColumnName = "Name" }
            }
        };

        Assert.IsFalse(settings.DeleteNotMatchedBySource, "DeleteNotMatchedBySource should default to false");
    }

    [TestMethod]
    public void TestSinkSettings_DeleteNotMatchedBySource_CanBeSetToTrue()
    {
        var settings = new SqlServerSinkSettings
        {
            ConnectionString = "Server=.;Database=Test;",
            TableName = "TestTable",
            WriteMode = SqlWriteMode.Upsert,
            PrimaryKeyColumns = new List<string> { "Id" },
            DeleteNotMatchedBySource = true,
            ColumnMappings = new List<ColumnMapping>
            {
                new ColumnMapping { ColumnName = "Id" },
                new ColumnMapping { ColumnName = "Name" }
            }
        };

        Assert.IsTrue(settings.DeleteNotMatchedBySource, "DeleteNotMatchedBySource should be settable to true");
    }

    [TestMethod]
    public void TestSinkSettings_DeleteNotMatchedBySource_DeserializesFromJson()
    {
        var json = """
        {
            "ConnectionString": "Server=.;Database=Test;",
            "TableName": "TestTable",
            "WriteMode": "Upsert",
            "PrimaryKeyColumns": ["Id"],
            "DeleteNotMatchedBySource": true,
            "ColumnMappings": [
                {"ColumnName": "Id"},
                {"ColumnName": "Name"}
            ]
        }
        """;
        
        var config = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
            .Build();
        var settings = config.Get<SqlServerSinkSettings>();
        
        Assert.IsNotNull(settings, "Settings should be deserialized");
        Assert.IsTrue(settings!.DeleteNotMatchedBySource, "DeleteNotMatchedBySource should be true");
    }

    [TestMethod]
    public void TestSinkSettings_DeleteNotMatchedBySource_WithInsertMode_FailsValidation()
    {
        var settings = new SqlServerSinkSettings
        {
            ConnectionString = "Server=.;Database=Test;",
            TableName = "TestTable",
            WriteMode = SqlWriteMode.Insert,
            DeleteNotMatchedBySource = true,
            ColumnMappings = new List<ColumnMapping>
            {
                new ColumnMapping { ColumnName = "Id" },
                new ColumnMapping { ColumnName = "Name" }
            }
        };

        var validationResults = settings.Validate(new ValidationContext(settings)).ToList();
        
        Assert.IsTrue(validationResults.Any(v => v.MemberNames.Contains(nameof(SqlServerSinkSettings.DeleteNotMatchedBySource))),
            "Validation should fail when DeleteNotMatchedBySource is true with Insert mode");
        
        Assert.IsTrue(validationResults.Any(v => v.ErrorMessage!.Contains("can only be used when WriteMode is Upsert")),
            "Validation error should mention Upsert mode requirement");
    }
}
