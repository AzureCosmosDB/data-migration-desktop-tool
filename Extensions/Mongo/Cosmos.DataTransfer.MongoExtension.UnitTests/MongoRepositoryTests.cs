using MongoDB.Bson;
using MongoDB.Driver;
using Moq;

namespace Cosmos.DataTransfer.MongoExtension.UnitTests;

[TestClass]
public class MongoRepositoryTests
{
    [TestMethod]
    public async Task FindAsync_WithPositiveBatchSize_PassesBatchSizeToDriver()
    {
        // Arrange
        var mockCollection = new Mock<IMongoCollection<BsonDocument>>();
        var mockCursor = new Mock<IAsyncCursor<BsonDocument>>();
        
        FindOptions<BsonDocument, BsonDocument>? capturedOptions = null;
        
        mockCollection
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<BsonDocument>>(),
                It.IsAny<FindOptions<BsonDocument, BsonDocument>>(),
                It.IsAny<CancellationToken>()))
            .Callback<FilterDefinition<BsonDocument>, FindOptions<BsonDocument, BsonDocument>, CancellationToken>(
                (filter, options, ct) => capturedOptions = options)
            .ReturnsAsync(mockCursor.Object);
        
        mockCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        
        mockCursor.Setup(c => c.Current).Returns(new List<BsonDocument> { new BsonDocument() });
        mockCursor.Setup(c => c.Dispose());
        
        var repository = new MongoRepository<BsonDocument>(mockCollection.Object);
        var filter = Builders<BsonDocument>.Filter.Empty;
        var batchSize = 1000;
        
        // Act
        var results = new List<BsonDocument>();
        await foreach (var doc in repository.FindAsync(filter, batchSize))
        {
            results.Add(doc);
        }
        
        // Assert
        Assert.IsNotNull(capturedOptions);
        Assert.AreEqual(batchSize, capturedOptions.BatchSize);
        Assert.AreEqual(1, results.Count);
    }
    
    [TestMethod]
    public async Task FindAsync_WithNullBatchSize_DoesNotSetBatchSizeOption()
    {
        // Arrange
        var mockCollection = new Mock<IMongoCollection<BsonDocument>>();
        var mockCursor = new Mock<IAsyncCursor<BsonDocument>>();
        
        FindOptions<BsonDocument, BsonDocument>? capturedOptions = null;
        
        mockCollection
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<BsonDocument>>(),
                It.IsAny<FindOptions<BsonDocument, BsonDocument>>(),
                It.IsAny<CancellationToken>()))
            .Callback<FilterDefinition<BsonDocument>, FindOptions<BsonDocument, BsonDocument>, CancellationToken>(
                (filter, options, ct) => capturedOptions = options)
            .ReturnsAsync(mockCursor.Object);
        
        mockCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        
        mockCursor.Setup(c => c.Current).Returns(new List<BsonDocument> { new BsonDocument() });
        mockCursor.Setup(c => c.Dispose());
        
        var repository = new MongoRepository<BsonDocument>(mockCollection.Object);
        var filter = Builders<BsonDocument>.Filter.Empty;
        
        // Act
        var results = new List<BsonDocument>();
        await foreach (var doc in repository.FindAsync(filter, null))
        {
            results.Add(doc);
        }
        
        // Assert
        Assert.IsNotNull(capturedOptions);
        Assert.IsNull(capturedOptions.BatchSize);
        Assert.AreEqual(1, results.Count);
    }
    
    [TestMethod]
    public async Task FindAsync_WithZeroBatchSize_DoesNotSetBatchSizeOption()
    {
        // Arrange
        var mockCollection = new Mock<IMongoCollection<BsonDocument>>();
        var mockCursor = new Mock<IAsyncCursor<BsonDocument>>();
        
        FindOptions<BsonDocument, BsonDocument>? capturedOptions = null;
        
        mockCollection
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<BsonDocument>>(),
                It.IsAny<FindOptions<BsonDocument, BsonDocument>>(),
                It.IsAny<CancellationToken>()))
            .Callback<FilterDefinition<BsonDocument>, FindOptions<BsonDocument, BsonDocument>, CancellationToken>(
                (filter, options, ct) => capturedOptions = options)
            .ReturnsAsync(mockCursor.Object);
        
        mockCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        
        mockCursor.Setup(c => c.Current).Returns(new List<BsonDocument> { new BsonDocument() });
        mockCursor.Setup(c => c.Dispose());
        
        var repository = new MongoRepository<BsonDocument>(mockCollection.Object);
        var filter = Builders<BsonDocument>.Filter.Empty;
        
        // Act
        var results = new List<BsonDocument>();
        await foreach (var doc in repository.FindAsync(filter, 0))
        {
            results.Add(doc);
        }
        
        // Assert
        Assert.IsNotNull(capturedOptions);
        Assert.IsNull(capturedOptions.BatchSize);
        Assert.AreEqual(1, results.Count);
    }
    
    [TestMethod]
    public async Task FindAsync_WithNegativeBatchSize_DoesNotSetBatchSizeOption()
    {
        // Arrange
        var mockCollection = new Mock<IMongoCollection<BsonDocument>>();
        var mockCursor = new Mock<IAsyncCursor<BsonDocument>>();
        
        FindOptions<BsonDocument, BsonDocument>? capturedOptions = null;
        
        mockCollection
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<BsonDocument>>(),
                It.IsAny<FindOptions<BsonDocument, BsonDocument>>(),
                It.IsAny<CancellationToken>()))
            .Callback<FilterDefinition<BsonDocument>, FindOptions<BsonDocument, BsonDocument>, CancellationToken>(
                (filter, options, ct) => capturedOptions = options)
            .ReturnsAsync(mockCursor.Object);
        
        mockCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        
        mockCursor.Setup(c => c.Current).Returns(new List<BsonDocument> { new BsonDocument() });
        mockCursor.Setup(c => c.Dispose());
        
        var repository = new MongoRepository<BsonDocument>(mockCollection.Object);
        var filter = Builders<BsonDocument>.Filter.Empty;
        
        // Act
        var results = new List<BsonDocument>();
        await foreach (var doc in repository.FindAsync(filter, -1))
        {
            results.Add(doc);
        }
        
        // Assert
        Assert.IsNotNull(capturedOptions);
        Assert.IsNull(capturedOptions.BatchSize);
        Assert.AreEqual(1, results.Count);
    }
}
