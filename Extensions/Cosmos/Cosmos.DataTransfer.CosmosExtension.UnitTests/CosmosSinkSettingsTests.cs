using Cosmos.DataTransfer.Interfaces;

namespace Cosmos.DataTransfer.CosmosExtension.UnitTests;

[TestClass]
public class CosmosSinkSettingsTests
{
    private static void LogErrors(IEnumerable<string?> errors)
    {
        foreach (var error in errors) Console.WriteLine($"Validation Error: {error}");
    }

    [TestMethod]
    public void GetValidationErrors_WithNoConnection_ReturnsError()
    {
        var settings = new CosmosSinkSettings
        {
            Database = "db",
            Container = "container",
        };

        var validationErrors = settings.GetValidationErrors();
        LogErrors(validationErrors);

        Assert.AreEqual(1, validationErrors.Count(v => v.Contains(nameof(CosmosSinkSettings.ConnectionString))));
    }

    [TestMethod]
    public void GetValidationErrors_WithNoRbacConnection_ReturnsError()
    {
        var settings = new CosmosSinkSettings
        {
            UseRbacAuth = true,
            Database = "db",
            Container = "container",
        };

        var validationErrors = settings.GetValidationErrors();
        LogErrors(validationErrors);

        Assert.AreEqual(1, validationErrors.Count(v => v.Contains(nameof(CosmosSinkSettings.AccountEndpoint))));
    }
    
    [TestMethod]
    public void Validate_WithConnectionString_Succeeds()
    {
        var settings = new CosmosSinkSettings
        {
            ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=",
            Database = "db",
            Container = "container",
        };

        settings.Validate();
    }

    [TestMethod]
    public void Validate_WithAccountEndpoint_Succeeds()
    {
        var settings = new CosmosSinkSettings
        {
            UseRbacAuth = true,
            AccountEndpoint = "https://localhost:8081/",
            Database = "db",
            Container = "container"
        };

        settings.Validate();
    }

    [TestMethod]
    public void GetValidationErrors_WhenRecreateContainerTrue_RequiresPartitionKeyPath()
    {
        var settings = new CosmosSinkSettings
        {
            ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=",
            Database = "db",
            Container = "container",
            RecreateContainer = true,
            WriteMode = DataWriteMode.Insert,
        };

        var validationErrors = settings.GetValidationErrors();
        LogErrors(validationErrors);

        Assert.AreEqual(1, validationErrors.Count(v => v.Contains(nameof(CosmosSinkSettings.PartitionKeyPath)) && v.Contains(nameof(CosmosSinkSettings.RecreateContainer))));
    }

    [TestMethod]
    public void GetValidationErrors_WhenWriteModeIsStream_RequiresPartitionKeyPath()
    {
        var settings = new CosmosSinkSettings
        {
            ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=",
            Database = "db",
            Container = "container",
            RecreateContainer = false,
            WriteMode = DataWriteMode.InsertStream,
        };

        var validationErrors = settings.GetValidationErrors();
        LogErrors(validationErrors);

        Assert.AreEqual(1, validationErrors.Count(v => v.Contains(nameof(CosmosSinkSettings.PartitionKeyPath)) && v.Contains(nameof(CosmosSinkSettings.WriteMode))));
    }

    [TestMethod]
    public void GetValidationErrors_WhenDbContainerMissing_ReturnsErrors()
    {
        var settings = new CosmosSinkSettings
        {
            ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=",
        };

        var validationErrors = settings.GetValidationErrors();
        LogErrors(validationErrors);

        Assert.AreEqual(1, validationErrors.Count(v => v.Contains(nameof(CosmosSinkSettings.Database))));
        Assert.AreEqual(1, validationErrors.Count(v => v.Contains(nameof(CosmosSinkSettings.Container))));
    }

    [TestMethod]
    public void GetValidationErrors_WhenRecreateContainerTrueAndWriteModeStreamWithPartitionKeys_Succeeds()
    {
        var settings = new CosmosSinkSettings
        {
            ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=",
            Database = "db",
            Container = "container",
            RecreateContainer = true,
            WriteMode = DataWriteMode.InsertStream,
            PartitionKeyPaths = new List<string> { "/a", "/b" },
        };

        var validationErrors = settings.GetValidationErrors();
        LogErrors(validationErrors);

        Assert.AreEqual(0, validationErrors.Count());
    }

    [TestMethod]
    public void GetValidationErrors_WhenPartitionKeysInvalid_ReturnsErrors()
    {
        var settings = new CosmosSinkSettings
        {
            ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=",
            Database = "db",
            Container = "container",
            RecreateContainer = true,
            PartitionKeyPaths = new List<string> { "a", "b" },
        };

        var validationErrors = settings.GetValidationErrors();
        LogErrors(validationErrors);

        Assert.AreEqual(1, validationErrors.Count(v => v.Contains(nameof(CosmosSinkSettings.PartitionKeyPaths))));
    }
}