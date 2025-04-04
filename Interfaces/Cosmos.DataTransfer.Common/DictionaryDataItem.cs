﻿using Cosmos.DataTransfer.Interfaces;

namespace Cosmos.DataTransfer.Common;

public class DictionaryDataItem : IDataItem
{
    public IDictionary<string, object?> Items { get; }

    public DictionaryDataItem(IDictionary<string, object?> items)
    {
        Items = items;
    }

    public IEnumerable<string> GetFieldNames()
    {
        return Items.Keys;
    }

    public object? GetValue(string fieldName)
    {
        return !Items.TryGetValue(fieldName, out var value) ? null : value;
    }
}
