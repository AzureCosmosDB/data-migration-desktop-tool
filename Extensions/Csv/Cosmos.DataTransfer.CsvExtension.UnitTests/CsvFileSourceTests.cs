using Cosmos.DataTransfer.Common.UnitTests;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cosmos.DataTransfer.CsvExtension.UnitTests;

[TestClass]
public class CsvFileSourceTests
{
    [TestMethod]
    public async Task ReadAsync_WithSimpleFile_ReadsRows()
    {
        CsvFileSource extension = new();
        var config = TestHelpers.CreateConfig(new Dictionary<string, string>
        {
            { "FilePath", "Data/SimpleData.csv" }
        });

        int counter = 0;
        int lastId = -1;
        await foreach (var dataItem in extension.ReadAsync(config, NullLogger.Instance))
        {
            counter++;
            CollectionAssert.AreEquivalent(new[] { "id", "name", "description", "count" }, dataItem.GetFieldNames().ToArray());
            object? value = dataItem.GetValue("id");
            Assert.IsNotNull(value);
            Assert.IsNotNull(dataItem.GetValue("name"));
            var current = Int32.Parse(value.ToString());
            Assert.IsTrue(current > lastId);
            lastId = current;
        }

        Assert.AreEqual(4, counter);
    }

    [TestMethod]
    public async Task ReadAsync_WithNoHeaders_ReadsRows()
    {
        CsvFileSource extension = new();
        var config = TestHelpers.CreateConfig(new Dictionary<string, string>
        {
            { "FilePath", "Data/NoHeaders.csv" },
            { "HasHeader", "false" },
            { "ColumnNameFormat", "myColumn{0}" },
        });

        int counter = 0;
        await foreach (var dataItem in extension.ReadAsync(config, NullLogger.Instance))
        {
            counter++;
            CollectionAssert.AreEquivalent(new[] { "myColumn0", "myColumn1", "myColumn2", "myColumn3" }, dataItem.GetFieldNames().ToArray());
            Assert.IsNotNull(dataItem.GetValue("myColumn0"));
        }

        Assert.AreEqual(3, counter);
    }
}