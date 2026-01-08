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

    [TestMethod]
    public void MongoSourceSettings_ShouldHaveBatchSizeProperty()
    {
        // Arrange & Act
        var settings = new MongoSourceSettings();
        
        // Assert
        Assert.IsNull(settings.BatchSize);
    }

    [TestMethod]
    public void MongoSourceSettings_ShouldAllowBatchSizeToBeSet()
    {
        // Arrange
        var settings = new MongoSourceSettings();
        var testBatchSize = 1000;
        
        // Act
        settings.BatchSize = testBatchSize;
        
        // Assert
        Assert.AreEqual(testBatchSize, settings.BatchSize);
    }

    [TestMethod]
    public void MongoSourceSettings_ShouldAllowNullBatchSize()
    {
        // Arrange
        var settings = new MongoSourceSettings();
        
        // Act
        settings.BatchSize = null;
        
        // Assert
        Assert.IsNull(settings.BatchSize);
    }

    [TestMethod]
    public void MongoSourceSettings_ShouldAcceptPositiveBatchSize()
    {
        // Arrange
        var settings = new MongoSourceSettings();
        var positiveBatchSizes = new[] { 1, 100, 1000, 10000 };
        
        // Act & Assert
        foreach (var batchSize in positiveBatchSizes)
        {
            settings.BatchSize = batchSize;
            Assert.AreEqual(batchSize, settings.BatchSize);
        }
    }
}