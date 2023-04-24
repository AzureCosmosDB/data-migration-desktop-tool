using Cosmos.DataTransfer.Interfaces;

namespace Cosmos.DataTransfer.CosmosExtension.UnitTests;

[TestClass]
public class CosmosSinkSettingsTests
{
    [TestMethod]
    public void GetValidationErrors_WithNoConnection_ReturnsError()
    {
        var settings = new CosmosSinkSettings
        {
            Database = "db",
            Container = "container",
        };

        var validationErrors = settings.GetValidationErrors();

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
            Container = "container",
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
        };

        var validationErrors = settings.GetValidationErrors();

        Assert.AreEqual(1, validationErrors.Count(v => v.Contains(nameof(CosmosSinkSettings.PartitionKeyPath))));
    }

    [TestMethod]
    public void GetValidationErrors_WhenDbContainerMissing_ReturnsErrors()
    {
        var settings = new CosmosSinkSettings
        {
            ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=",
        };

        var validationErrors = settings.GetValidationErrors();

        Assert.AreEqual(1, validationErrors.Count(v => v.Contains(nameof(CosmosSinkSettings.Database))));
        Assert.AreEqual(1, validationErrors.Count(v => v.Contains(nameof(CosmosSinkSettings.Container))));
    }
}