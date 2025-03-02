using System.Diagnostics.CodeAnalysis;
using Moq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cosmos.DataTransfer.Common.UnitTests;

/// <summary>
/// Method for suppling Assert.AreEqual with a custom equality comparer
/// (IEqualityComparer).
/// </summary>
public static class AssertExtension {
    public static void AreEqual<T>(this Assert that, T expected, T actual, IEqualityComparer<T> comparer) {
        if (!comparer.Equals(expected, actual))
            throw new AssertFailedException(
                $"Assert.AreEqual failed. Expected:<{expected!}>. Actual:<{actual!}>.");
    }
}

[TestClass]
public class AssertExtensionTests {
    [TestMethod]
    public void AreEqual_ThrowsArgumentNullException() {
        var bad_comparer = new Mock<IEqualityComparer<int>>();
        bad_comparer.Setup((x) => x.Equals(It.IsAny<int>(), It.IsAny<int>()))
            .Returns(false);
        var e = Assert.ThrowsException<AssertFailedException>(
            () => Assert.That.AreEqual(1, 1, bad_comparer.Object)
        );
        Assert.AreEqual("Assert.AreEqual failed. Expected:<1>. Actual:<1>.", e.Message);

        var good_comparer = new Mock<IEqualityComparer<int>>();
        good_comparer.Setup((x) => x.Equals(It.IsAny<int>(), It.IsAny<int>()))
            .Returns(true);
        Assert.That.AreEqual(1, 1, good_comparer.Object); // No Exceptions thrown
    }
}

