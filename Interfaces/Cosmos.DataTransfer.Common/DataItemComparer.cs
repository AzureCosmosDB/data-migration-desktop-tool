using System.Diagnostics.CodeAnalysis;
using Cosmos.DataTransfer.Interfaces;

namespace Cosmos.DataTransfer.Common;

/// <summary>
/// Comparer for `DictionaryDataItem`s that compares fields and values,
/// where nulls are considered equal (optional).
/// One-sided nulls are never equal.
/// </summary>
public class DataItemComparer : IEqualityComparer<IDataItem>
{
    public bool NullsAreEqual { get; }
    public DataItemComparer(bool nullsAreEqual = true) {
        NullsAreEqual = nullsAreEqual;
    }

    public bool Equals(IDataItem? x, IDataItem? y)
    {
        if (x == y) return true;
        if (x == null || y == null) return false;

        var xFields = x.GetFieldNames().ToHashSet();
        var yFields = y.GetFieldNames().ToHashSet();

        if (xFields.Count == 0 && yFields.Count == 0) {
            return true;
        } else if (xFields.Count == 0 || yFields.Count == 0) {
            return false;
        }

        // HashSet.SetEquals is *not* symmetric.
        if (!xFields.SetEquals(yFields) || !yFields.SetEquals(xFields)) return false;

        foreach (var key in x.GetFieldNames()) {
            object? xValue = x.GetValue(key);
            object? yValue = y.GetValue(key);
            if (xValue == null && yValue == null && !NullsAreEqual) return false;
            if (xValue == null ^ yValue ==  null) return false;
            if (xValue != null && !xValue.Equals(yValue)) return false;
        }
        return true;
    }

    public int GetHashCode([DisallowNull] IDataItem obj)
    {
        var hash = new HashCode();
        foreach (var key in obj.GetFieldNames()) {
            hash.Add(key);
            hash.Add(obj.GetValue(key));
        }
        return hash.ToHashCode();
    }
}