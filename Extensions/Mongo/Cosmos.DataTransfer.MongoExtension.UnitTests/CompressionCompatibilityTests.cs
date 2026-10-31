using System.IO.Compression;
using System.Reflection;
using MongoDB.Driver;

namespace Cosmos.DataTransfer.MongoExtension.UnitTests;

[TestClass]
public class CompressionCompatibilityTests
{
    [DataTestMethod]
    [DataRow("SnappyCompressor", -1)]
    [DataRow("ZlibCompressor", -1)]
    [DataRow("ZlibCompressor", 0)]
    [DataRow("ZlibCompressor", 1)]
    [DataRow("ZlibCompressor", 6)]
    [DataRow("ZlibCompressor", 9)]
    public void DriverCompressor_RoundTripsWithServicedDependencies(string name, int level)
    {
        // Exercise the driver's compiled call sites, not just the new library APIs.
        var type = typeof(MongoClient).Assembly.GetType($"MongoDB.Driver.Core.Compression.{name}", throwOnError: true)!;
        var compressor = Activator.CreateInstance(type, name == "ZlibCompressor" ? new object[] { level } : Array.Empty<object>())!;
        var compress = type.GetMethod("Compress", BindingFlags.Instance | BindingFlags.Public)!;
        var decompress = type.GetMethod("Decompress", BindingFlags.Instance | BindingFlags.Public)!;
        byte[] expected = Enumerable.Range(0, 140000).Select(i => (byte)(i % 251)).ToArray();
        using var input = new MemoryStream(expected);
        using var compressed = new MemoryStream();
        using var actual = new MemoryStream();

        compress.Invoke(compressor, new object[] { input, compressed });
        Assert.IsTrue(compressed.Length > 0);
        compressed.Position = 0;
        decompress.Invoke(compressor, new object[] { compressed, actual });
        CollectionAssert.AreEqual(expected, actual.ToArray());
        Assert.IsTrue(input.CanRead);
        Assert.IsTrue(compressed.CanRead);

        if (name == "ZlibCompressor")
        {
            compressed.Position = 0;
            using var frameworkDecompressor = new ZLibStream(compressed, CompressionMode.Decompress, leaveOpen: true);
            using var frameworkOutput = new MemoryStream();
            frameworkDecompressor.CopyTo(frameworkOutput);
            CollectionAssert.AreEqual(expected, frameworkOutput.ToArray());

            using var frameworkCompressed = new MemoryStream();
            using (var frameworkCompressor = new ZLibStream(frameworkCompressed, CompressionLevel.Optimal, leaveOpen: true))
            {
                frameworkCompressor.Write(expected);
            }
            frameworkCompressed.Position = 0;
            actual.SetLength(0);
            decompress.Invoke(compressor, new object[] { frameworkCompressed, actual });
            CollectionAssert.AreEqual(expected, actual.ToArray());
        }
    }
}
