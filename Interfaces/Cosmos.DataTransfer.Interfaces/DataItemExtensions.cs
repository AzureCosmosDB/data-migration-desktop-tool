using System.Dynamic;

namespace Cosmos.DataTransfer.Interfaces;

public static class DataItemExtensions
{
    /// <summary>
    /// Given a source IDataItem, builds a dynamic object tree including child objects and arrays
    /// </summary>
    /// <param name="source"></param>
    /// <param name="requireStringId">If true, adds a new GUID "id" field to any top level items where one is not already present.</param>
    /// <param name="ignoreNullValues">If true, excludes fields containing null values from output.</param>
    /// <param name="preserveMixedCaseIds">If true, disregards differently cased "id" fields for purposes of required "id" and passes them through.</param>
    /// <param name="transformations">List of transformations to be performed</param>
    /// <returns>A dynamic object containing the entire data structure.</returns>
    /// <remarks>The returned ExpandoObject can be used directly as an IDictionary.</remarks>
    public static ExpandoObject? BuildDynamicObjectTree(this IDataItem? source, bool requireStringId = false, bool ignoreNullValues = false, bool preserveMixedCaseIds = false, Dictionary<string, DataItemTransformation>? transformations = null)
    {
        if (source == null) 
        {
            return null;
        }

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
        var containsIdIdFieldTransformation = transformations?.Values.Any(t => t.DestinationFieldName?.ToLowerInvariant() == "id") ?? false;

        if (requireStringId)
        {
            bool mismatchedIdCasing = preserveMixedCaseIds && !containsLowercaseIdField;
            if ((!containsAnyIdField || mismatchedIdCasing) && !containsIdIdFieldTransformation)
            {
                item.TryAdd("id", Guid.NewGuid().ToString());
            }
        }

        foreach (string field in fields)
        {
            object? value = source.GetValue(field);

            if (ignoreNullValues && value == null) 
            {
                continue;
            }

            DataItemTransformation? itemTransformation = null;
            transformations?.TryGetValue(field, out itemTransformation);

            var fieldName = itemTransformation?.DestinationFieldName ?? field;
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
                value = BuildDynamicObjectTree(child, ignoreNullValues: ignoreNullValues);
            }
            else if (value is IEnumerable<object?> array)
            {
                value = BuildArray(array, ignoreNulls: ignoreNullValues);
            }

            if(string.IsNullOrEmpty(itemTransformation?.DestinationFieldTypeCode))
            {
                item.TryAdd(fieldName, value);
            }
            else
            {
                Enum.TryParse<TypeCode>(itemTransformation.DestinationFieldTypeCode, true, out TypeCode typeCode);
                item.TryAdd(fieldName, Convert.ChangeType(value, typeCode));
            }
        }

        return item;

        static object BuildArray(IEnumerable<object?> array, bool ignoreNulls)
        {
            return array.Select(dataItem =>
            {
                switch (dataItem)
                {
                    case IDataItem childObject:
                        return BuildDynamicObjectTree(childObject, ignoreNullValues: ignoreNulls);
                    case IEnumerable<object?> array:
                        return BuildArray(array, ignoreNulls);
                    default:
                        return dataItem;
                }
            }).ToArray();
        }
    }
}