using System;
using System.Collections.Generic;
using System.Linq;

namespace Cosmos.DataTransfer.App.Windows;

public static class CollectionExtensions
{
    public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }
}