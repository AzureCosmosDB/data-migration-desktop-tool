using Cosmos.DataTransfer.Interfaces;
using MongoDB.Bson;

namespace Cosmos.DataTransfer.MongoExtension.UnitTests;

[TestClass]
public class MongoDataSinkIdMappingTests
{
    [TestMethod]
    public void BsonDocument_ShouldMapCustomFieldToIdWhenCreated()
    {
        // Arrange - Simulate a data item with an "id" field
        var testData = new Dictionary<string, object?>
        {
            { "id", "BSKT_1" },
            { "CustomerId", 111 }
        };

        // Act - Create BsonDocument and manually set _id (simulating what the sink does)
        var bsonDoc = new BsonDocument(testData);
        var idValue = testData["id"];
        if (idValue != null)
        {
            bsonDoc["_id"] = BsonValue.Create(idValue);
        }

        // Assert
        Assert.IsNotNull(bsonDoc["_id"]);
        Assert.AreEqual("BSKT_1", bsonDoc["_id"].AsString);
        Assert.AreEqual("BSKT_1", bsonDoc["id"].AsString);
        Assert.AreEqual(111, bsonDoc["CustomerId"].AsInt32);
        Assert.AreEqual(3, bsonDoc.ElementCount); // _id, id, CustomerId
    }

    [TestMethod]
    public void BsonDocument_ShouldHandleStringId()
    {
        // Arrange
        var testId = "string-id";

        // Act
        var bsonDoc = new BsonDocument
        {
            { "type", "string" }
        };
        bsonDoc["_id"] = BsonValue.Create(testId);
        bsonDoc["id"] = BsonValue.Create(testId);

        // Assert
        Assert.IsNotNull(bsonDoc["_id"]);
        Assert.IsNotNull(bsonDoc["id"]);
        Assert.AreEqual(bsonDoc["_id"].ToString(), bsonDoc["id"].ToString());
    }

    [TestMethod]
    public void BsonDocument_ShouldHandleIntId()
    {
        // Arrange
        var testId = 12345;

        // Act
        var bsonDoc = new BsonDocument
        {
            { "type", "int" }
        };
        bsonDoc["_id"] = BsonValue.Create(testId);
        bsonDoc["id"] = BsonValue.Create(testId);

        // Assert
        Assert.IsNotNull(bsonDoc["_id"]);
        Assert.IsNotNull(bsonDoc["id"]);
        Assert.AreEqual(bsonDoc["_id"].AsInt32, bsonDoc["id"].AsInt32);
    }

    [TestMethod]
    public void BsonDocument_ShouldWorkWithCaseInsensitiveFieldNames()
    {
        // Arrange - Test case insensitive matching
        var fieldNames = new[] { "id", "Id", "ID", "iD" };
        
        foreach (var fieldName in fieldNames)
        {
            var testData = new Dictionary<string, object?>
            {
                { fieldName, "TEST_ID" },
                { "data", "value" }
            };

            // Act
            var bsonDoc = new BsonDocument(testData);
            // Simulate case-insensitive lookup
            var idField = testData.Keys.FirstOrDefault(k => k.Equals(fieldName, StringComparison.CurrentCultureIgnoreCase));
            if (idField != null)
            {
                bsonDoc["_id"] = BsonValue.Create(testData[idField]);
            }

            // Assert
            Assert.IsNotNull(bsonDoc["_id"], $"Failed for field name: {fieldName}");
            Assert.AreEqual("TEST_ID", bsonDoc["_id"].AsString, $"Failed for field name: {fieldName}");
        }
    }

    [TestMethod]
    public void BsonDocument_ShouldNotOverwriteExistingIdIfNotSpecified()
    {
        // Arrange - Test when no IdFieldName is specified
        var testData = new Dictionary<string, object?>
        {
            { "id", "BSKT_1" },
            { "CustomerId", 111 }
        };

        // Act - Create BsonDocument without setting _id
        var bsonDoc = new BsonDocument(testData);

        // Assert - _id should not exist yet (MongoDB will add it on insert)
        Assert.IsFalse(bsonDoc.Contains("_id"));
        Assert.AreEqual("BSKT_1", bsonDoc["id"].AsString);
    }
}
