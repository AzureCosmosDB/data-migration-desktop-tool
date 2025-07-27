using Cosmos.DataTransfer.MongoExtension.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using MongoDB.Bson;

namespace Cosmos.DataTransfer.MongoExtension.UnitTests;

[TestClass]
public class MongoDataSourceExtensionTests
{
    private Mock<ILogger> _mockLogger;
    private MongoDataSourceExtension _extension;

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger>();
        _extension = new MongoDataSourceExtension();
    }

    [TestMethod]
    public void MongoDataSourceExtension_ShouldHaveDisplayName()
    {
        // Assert
        Assert.AreEqual("MongoDB", _extension.DisplayName);
    }

    [TestMethod]
    public void MongoDataSourceExtension_ShouldReturnSettings()
    {
        // Act
        var settings = _extension.GetSettings();

        // Assert
        Assert.IsNotNull(settings);
        Assert.IsTrue(settings.Any());
        Assert.IsInstanceOfType(settings.First(), typeof(MongoSourceSettings));
    }

    [TestMethod]
    public void BsonDocumentParsing_ShouldWorkWithValidJson()
    {
        // Arrange
        var jsonQuery = """{"status": "active", "type": "user"}""";

        // Act & Assert - This should not throw
        var bsonDoc = BsonDocument.Parse(jsonQuery);
        Assert.IsNotNull(bsonDoc);
        Assert.AreEqual("active", bsonDoc["status"].AsString);
        Assert.AreEqual("user", bsonDoc["type"].AsString);
    }

    [TestMethod]
    public void BsonDocumentParsing_ShouldWorkWithComplexQuery()
    {
        // Arrange
        var jsonQuery = """{"timestamp":{"$gte":"2025-01-01","$lt":"2025-02-01"}}""";

        // Act & Assert - This should not throw
        var bsonDoc = BsonDocument.Parse(jsonQuery);
        Assert.IsNotNull(bsonDoc);
        Assert.IsTrue(bsonDoc.Contains("timestamp"));
        
        var timestampFilter = bsonDoc["timestamp"].AsBsonDocument;
        Assert.AreEqual("2025-01-01", timestampFilter["$gte"].AsString);
        Assert.AreEqual("2025-02-01", timestampFilter["$lt"].AsString);
    }

    [TestMethod]
    [ExpectedException(typeof(FormatException))]
    public void BsonDocumentParsing_ShouldThrowOnInvalidJson()
    {
        // Arrange
        var invalidJsonQuery = """{"status": "active", "type": }"""; // Invalid JSON

        // Act & Assert - This should throw
        BsonDocument.Parse(invalidJsonQuery);
    }

    [TestMethod]
    public void QueryFileReading_ShouldWorkWithValidFile()
    {
        // Arrange
        var testFilePath = Path.Combine("Data", "simple-query.json");
        
        // Act & Assert - File should exist and be readable
        Assert.IsTrue(File.Exists(testFilePath));
        var content = File.ReadAllText(testFilePath);
        Assert.IsFalse(string.IsNullOrWhiteSpace(content));
        
        // Should be valid JSON that can be parsed to BSON
        var bsonDoc = BsonDocument.Parse(content);
        Assert.IsNotNull(bsonDoc);
    }

    [TestMethod]
    public void QueryFileReading_ShouldWorkWithComplexQueryFile()
    {
        // Arrange
        var testFilePath = Path.Combine("Data", "date-range-query.json");
        
        // Act & Assert - File should exist and be readable
        Assert.IsTrue(File.Exists(testFilePath));
        var content = File.ReadAllText(testFilePath);
        Assert.IsFalse(string.IsNullOrWhiteSpace(content));
        
        // Should be valid JSON that can be parsed to BSON
        var bsonDoc = BsonDocument.Parse(content);
        Assert.IsNotNull(bsonDoc);
        Assert.IsTrue(bsonDoc.Contains("timestamp"));
    }
}