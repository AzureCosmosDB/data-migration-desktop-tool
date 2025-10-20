using Cosmos.DataTransfer.MongoExtension.Settings;

namespace Cosmos.DataTransfer.MongoExtension.UnitTests;

[TestClass]
public class MongoSinkSettingsTests
{
    [TestMethod]
    public void MongoSinkSettings_ShouldHaveIdFieldNameProperty()
    {
        // Arrange & Act
        var settings = new MongoSinkSettings();
        
        // Assert
        Assert.IsNull(settings.IdFieldName);
    }

    [TestMethod]
    public void MongoSinkSettings_ShouldAllowIdFieldNameToBeSet()
    {
        // Arrange
        var settings = new MongoSinkSettings();
        var testFieldName = "id";
        
        // Act
        settings.IdFieldName = testFieldName;
        
        // Assert
        Assert.AreEqual(testFieldName, settings.IdFieldName);
    }

    [TestMethod]
    public void MongoSinkSettings_ShouldAllowNullIdFieldName()
    {
        // Arrange
        var settings = new MongoSinkSettings();
        
        // Act
        settings.IdFieldName = null;
        
        // Assert
        Assert.IsNull(settings.IdFieldName);
    }

    [TestMethod]
    public void MongoSinkSettings_ShouldAllowEmptyIdFieldName()
    {
        // Arrange
        var settings = new MongoSinkSettings();
        
        // Act
        settings.IdFieldName = string.Empty;
        
        // Assert
        Assert.AreEqual(string.Empty, settings.IdFieldName);
    }

    [TestMethod]
    public void MongoSinkSettings_ShouldSupportCaseInsensitiveFieldName()
    {
        // Arrange
        var settings = new MongoSinkSettings();
        var testFieldNames = new[] { "id", "Id", "ID", "iD" };
        
        // Act & Assert
        foreach (var fieldName in testFieldNames)
        {
            settings.IdFieldName = fieldName;
            Assert.AreEqual(fieldName, settings.IdFieldName);
        }
    }
}
