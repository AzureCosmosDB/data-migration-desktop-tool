using Cosmos.DataTransfer.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cosmos.DataTransfer.Common.UnitTests;

[TestClass]
public class DictionaryDataItemComparerTests 
{

    [TestMethod]
    public void Test_ClassNulls() {
        var x = new DictionaryDataItem(
            new Dictionary<string, object?> { { "foo", 1 }}
        );
        var comparer = new DataItemComparer();

        Assert.IsTrue(comparer.Equals(x, x));
        Assert.AreEqual(comparer.GetHashCode(x), comparer.GetHashCode(x));
        
        Assert.IsFalse(comparer.Equals(x, null));
        Assert.IsFalse(comparer.Equals(null, x));
        Assert.IsTrue(comparer.Equals(null, null));
    }

    [TestMethod]
    public void TestEquals_0() {
        var x = new DictionaryDataItem(
            new Dictionary<string, object?> { }
        );
        var y = new DictionaryDataItem(
            new Dictionary<string, object?> { }
        );
        var comparer = new DataItemComparer();

        Assert.IsTrue(comparer.Equals(x, y));
        Assert.AreEqual(comparer.GetHashCode(x), comparer.GetHashCode(y));

        x.Items.Add("foo", 1);
        Assert.IsFalse(comparer.Equals(x, y));
        Assert.IsFalse(comparer.Equals(y, x));
    }



    [TestMethod]
    public void TestEquals_1() {
        var x = new DictionaryDataItem(
            new Dictionary<string, object?> { { "foo", 1 }, { "bat", "man" } }
        );
        var y = new DictionaryDataItem(
            new Dictionary<string, object?> { { "foo", 1 }, { "bat", "man" } }
        );
        var comparer = new DataItemComparer();

        Assert.IsTrue(comparer.Equals(x, y));
        Assert.AreEqual(comparer.GetHashCode(x), comparer.GetHashCode(y));

        x.Items.Add("other", "foobar");
        Assert.IsFalse(comparer.Equals(x, y));
        
        y.Items.Add("other", "bar");
        Assert.IsFalse(comparer.Equals(x, y));
        
        y.Items["other"] = x.Items["other"];
        Assert.IsTrue(comparer.Equals(x, y));
        Assert.AreEqual(comparer.GetHashCode(x), comparer.GetHashCode(y));
    }

    [TestMethod]
    public void TestEquals_2() {
        var x = new DictionaryDataItem(
            new Dictionary<string, object?> { { "foo", 1 }}
        );
        var y = new DictionaryDataItem(
            new Dictionary<string, object?> { { "bar", 1 }}
        );
        var comparer = new DataItemComparer();

        Assert.IsFalse(comparer.Equals(x, y));
    }

    [TestMethod]
    public void TestEquals_ItemNulls() {
        var x = new DictionaryDataItem(
            new Dictionary<string, object?> { { "foo", 1 }}
        );
        var y = new DictionaryDataItem(
            new Dictionary<string, object?> { { "foo", null }}
        );
        var comparer = new DataItemComparer();

        Assert.IsFalse(comparer.Equals(x, y));
        x.Items["foo"] = null;
        Assert.IsTrue(comparer.Equals(x, y));
        Assert.AreEqual(comparer.GetHashCode(x), comparer.GetHashCode(y));
        y.Items["foo"] = 1;
        Assert.IsFalse(comparer.Equals(x, y));
    }

    [TestMethod]
    public void TestEquals_ItemNulls_NotEqual() {
        var x = new DictionaryDataItem(
            new Dictionary<string, object?> { { "foo", null }}
        );
        var y = new DictionaryDataItem(
            new Dictionary<string, object?> { { "foo", null }}
        );
        var comparer = new DataItemComparer(nullsAreEqual: false);

        Assert.IsFalse(comparer.Equals(x, y));
    }

    [TestMethod]
    public void TestGetFieldNames() {
        var x = new DictionaryDataItem(new Dictionary<string, object?>());
        Assert.AreEqual(0, x.GetFieldNames().Count());
        x.Items["foo"] = 2;
        x.Items["bar"] = "zoo";
        CollectionAssert.AreEqual(new string[] { "foo", "bar" }, x.GetFieldNames().ToArray());
    }

    [TestMethod]
    public void TestGetValue() {
        var x = new DictionaryDataItem(new Dictionary<string, object?>());
        Assert.IsNull(x.GetValue("foo"));
        Assert.IsNull(x.GetValue("1"));
        Assert.ThrowsException<ArgumentNullException>(() => x.GetValue(null!));

        x.Items["foo"] = 2;
        x.Items["bar"] = "zoo";
        x.Items["egg"] = null;

        Assert.AreEqual("zoo", x.GetValue("bar"));
        Assert.AreEqual(2, x.GetValue("foo"));
        Assert.IsNull(x.GetValue("egg"));
    }
}
