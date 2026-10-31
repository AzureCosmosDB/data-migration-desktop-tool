using Parquet;
using Parquet.Data;
using Parquet.Schema;

namespace Cosmos.DataTransfer.ParquetExtension.UnitTests;

[TestClass]
public class CompressionCompatibilityTests
{
    [TestMethod]
    public async Task Snappy_RoundTripsWithServicedDependency()
    {
        var field = new DataField<string>("value");
        var schema = new ParquetSchema(field);
        var expected = Enumerable.Range(0, 10000).Select(i => $"row-{i % 251}-compression").ToArray();
        using var stream = new MemoryStream();
        using (var writer = await ParquetWriter.CreateAsync(schema, stream))
        {
            writer.CompressionMethod = CompressionMethod.Snappy;
            using var group = writer.CreateRowGroup();
            await group.WriteColumnAsync(new DataColumn(field, expected));
        }

        stream.Position = 0;
        using var reader = await ParquetReader.CreateAsync(stream);
        using var rowGroup = reader.OpenRowGroupReader(0);
        var actual = await rowGroup.ReadColumnAsync(reader.Schema.GetDataFields()[0]);
        CollectionAssert.AreEqual(expected, actual.Data);
    }
}
