using System.Dynamic;

namespace Cosmos.DataTransfer.Interfaces;

public static class DataItemExtensions
{
    /// <summary>
    /// Given a source IDataItem, builds a dynamic object tree including child objects and arrays
    /// </summary>
    /// <param name="source"></param>
    /// <param name="requireStringId">If true, adds a new GUID "id" field to any top level items where one is not already present.</param>
    /// <param name="preserveMixedCaseIds">If true, disregards differently cased "id" fields for purposes of required "id" and passes them through.</param>
    /// <returns>A dynamic object containing the entire data structure.</returns>
    /// <remarks>The returned ExpandoObject can be used directly as an IDictionary.</remarks>
    public static ExpandoObject? BuildDynamicObjectTree(this IDataItem? source, bool requireStringId = false, bool preserveMixedCaseIds = false)
    {
        if (source == null)
            return null;

        var fields = source.GetFieldNames().ToList();
        var item = new ExpandoObject();

        /*
         * If the item contains a lowercase id field, we can take it as is.
         * If we have an uppercase Id or ID field, but no lowercase id, we will rename it to id, unless `preserveMixedCaseIds` is set to true.
         * If `preserveMixedCaseIds` is set to true, any differently cased "id" fields will be passed through as normal properties with no casing change and a separate "id" will be generated.
         * Then it can be used i.e. as CosmosDB primary key, when `requireStringId` is set to true.         
         */
        var containsLowercaseIdField = fields.Contains("id", StringComparer.CurrentCulture);
        var containsAnyIdField = fields.Contains("id", StringComparer.CurrentCultureIgnoreCase);

        if (requireStringId)
        {
            bool mismatchedIdCasing = preserveMixedCaseIds && !containsLowercaseIdField;
            if (!containsAnyIdField || mismatchedIdCasing)
            {
                item.TryAdd("id", Guid.NewGuid().ToString());
            }
        }

        foreach (string field in fields)
        {
            object? value = source.GetValue(field);
            var fieldName = field;
            if (requireStringId && string.Equals(field, "id", StringComparison.CurrentCultureIgnoreCase))
            {
                if (preserveMixedCaseIds)
                {
                    if (string.Equals(field, "id", StringComparison.CurrentCulture))
                    {
                        value = value?.ToString();
                    }
                }
                else if (!containsLowercaseIdField)
                {
                    value = value?.ToString();
                    fieldName = "id";
                }
                else
                {
                    value = value?.ToString();
                }
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