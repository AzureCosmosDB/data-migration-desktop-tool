using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cosmos.DataTransfer.Common.UnitTests;

/// <summary>
/// Method for suppling CollectionAssert.AreEqual with a custom equality comparer
/// (IEqualityComparer).
/// </summary>
public static class CollectionAssertExtension {
    public static void AreEqual<T>(this CollectionAssert that, IEnumerable<T> expected, IEnumerable<T> actual, IEqualityComparer<T> comparer) 
    { 
        var exp = expected.GetEnumerator();
        var act = actual.GetEnumerator();
        int i = 0;
        while (exp.MoveNext()) {
            if (!act.MoveNext()) {
                throw new AssertFailedException($"Actual is shorter than expected, missing index {i}.");
            }
            if (!comparer.Equals(act.Current, exp.Current)) {
                throw new AssertFailedException($"Mismatch at index {i}: Actual: <{act.Current?.ToString() ?? "null"}>, Expected: <{exp.Current?.ToString()}>.");
            }
            i++;
        }
        if (act.MoveNext()) {
            throw new AssertFailedException("Actual is longer than expected.");
        }
    }
}

[TestClass]
public class CollectionAssertExtensionTests {
    private class IntComparer : IEqualityComparer<int?>
    {
        public bool Equals(int? x, int? y) {
            return x == y;
        }

        public int GetHashCode([DisallowNull] int? obj)
        {
            return obj!.GetHashCode();
        }
    }

    [TestMethod]
    public void TestAreEqual() {
        var x = new int?[] { 1, 2, 3 };
        
        CollectionAssert.That.AreEqual(x, x, new IntComparer());
    }

    public static IEnumerable<object[]> AreEqualFailData { get {
        yield return new object[] { new int?[] { }, "Actual is shorter than expected, missing index 0." };
        yield return new object[] { new int?[] { 1 }, "Actual is shorter than expected, missing index 1." };
        yield return new object[] { new int?[] { 1, 3 }, "Mismatch at index 1: Actual: <3>, Expected: <2>." };
        yield return new object[] { new int?[] { 1, 2, 4 }, "Mismatch at index 2: Actual: <4>, Expected: <3>." };
        yield return new object[] { new int?[] { 1, 2, 3, 5 }, "Actual is longer than expected." };
        yield return new object[] { new int?[] { 1, 2, null }, "Mismatch at index 2: Actual: <null>, Expected: <3>." };
    }}

    [TestMethod]
    [DynamicData(nameof(AreEqualFailData))]
    public void TestAreEqual_Fails(int?[] actual, string message) {
        var x = new int?[] { 1, 2, 3 };
        
        AssertFailedException e;
        e = Assert.ThrowsException<AssertFailedException>(() => 
            CollectionAssert.That.AreEqual(x, actual, new IntComparer()));
        Assert.AreEqual(message, e.Message);
    }
}