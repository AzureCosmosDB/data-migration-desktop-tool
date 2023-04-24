using Cosmos.DataTransfer.Interfaces;

namespace Cosmos.DataTransfer.CosmosExtension.UnitTests;

[TestClass]
public class CosmosSourceSettingsTests
{
    [TestMethod]
    public void GetValidationErrors_WithNoConnection_ReturnsError()
    {
        var settings = new CosmosSourceSettings
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
        var settings = new CosmosSourceSettings
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
        var settings = new CosmosSourceSettings
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
        var settings = new CosmosSourceSettings
        {
            UseRbacAuth = true,
            AccountEndpoint = "https://localhost:8081/",
            Database = "db",
            Container = "container",
        };

        settings.Validate();
    }
}