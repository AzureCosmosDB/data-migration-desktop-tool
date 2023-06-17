using System.Dynamic;

namespace Cosmos.DataTransfer.Interfaces;

public static class DataItemExtensions
{
    /// <summary>
    /// Given a source IDataItem, builds a dynamic object tree including child objects and arrays
    /// </summary>
    /// <param name="source"></param>
    /// <param name="requireStringId">If true, adds a new GUID "id" field to any top level items where one is not already present.</param>
    /// <returns>A dynamic object containing the entire data structure.</returns>
    /// <remarks>The returned ExpandoObject can be used directly as an IDictionary.</remarks>
    public static ExpandoObject? BuildDynamicObjectTree(this IDataItem? source, bool requireStringId = false)
    {
        if (source == null)
            return null;

        var fields = source.GetFieldNames().ToList();
        var item = new ExpandoObject();
        if (requireStringId && !fields.Contains("id", StringComparer.CurrentCultureIgnoreCase))
        {
            item.TryAdd("id", Guid.NewGuid().ToString());
        }
        foreach (string field in fields)
        {
            object? value = source.GetValue(field);
            var fieldName = field;
            if (string.Equals(field, "id", StringComparison.CurrentCultureIgnoreCase) && requireStringId)
            {
                value = value?.ToString();
                fieldName = "id";
            }
            else if (value is IDataItem child)
            {
                value = BuildDynamicObjectTree(child);
            }
            else if (value is IEnumerable<object?> array)
            {
                value = BuildArray(array);
            }

            item.TryAdd(fieldName, value);
        }

        return item;

        static object BuildArray(IEnumerable<object?> array)
        {
            return array.Select(dataItem =>
            {
                switch (dataItem)
                {
                    case IDataItem childObject:
                        return BuildDynamicObjectTree(childObject);
                    case IEnumerable<object?> array:
                        return BuildArray(array);
                    default:
                        return dataItem;
                }
            }).ToArray();
        }
    }
}