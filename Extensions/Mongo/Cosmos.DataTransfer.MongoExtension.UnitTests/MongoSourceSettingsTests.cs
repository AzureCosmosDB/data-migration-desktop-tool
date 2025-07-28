using Cosmos.DataTransfer.MongoExtension.Settings;

namespace Cosmos.DataTransfer.MongoExtension.UnitTests;

[TestClass]
public class MongoSourceSettingsTests
{
    [TestMethod]
    public void MongoSourceSettings_ShouldHaveQueryProperty()
    {
        // Arrange & Act
        var settings = new MongoSourceSettings();
        
        // Assert
        Assert.IsNull(settings.Query);
    }

    [TestMethod]
    public void MongoSourceSettings_ShouldAllowQueryToBeSet()
    {
        // Arrange
        var settings = new MongoSourceSettings();
        var testQuery = """{"status": "active"}""";
        
        // Act
        settings.Query = testQuery;
        
        // Assert
        Assert.AreEqual(testQuery, settings.Query);
    }

    [TestMethod]
    public void MongoSourceSettings_ShouldAllowNullQuery()
    {
        // Arrange
        var settings = new MongoSourceSettings();
        
        // Act
        settings.Query = null;
        
        // Assert
        Assert.IsNull(settings.Query);
    }

    [TestMethod]
    public void MongoSourceSettings_ShouldAllowEmptyQuery()
    {
        // Arrange
        var settings = new MongoSourceSettings();
        
        // Act
        settings.Query = string.Empty;
        
        // Assert
        Assert.AreEqual(string.Empty, settings.Query);
    }
}