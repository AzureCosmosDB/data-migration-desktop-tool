using System.Collections;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Cosmos.DataTransfer.Interfaces;

namespace Cosmos.DataTransfer.Common;

public static class DataItemJsonConverter
{
    public static string AsJsonString(this IDataItem dataItem, bool indented, bool includeNullFields)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = indented }))
        {
            WriteDataItem(writer, dataItem, includeNullFields);
        }

        var bytes = stream.ToArray();
        return Encoding.UTF8.GetString(bytes);
    }

    public static void WriteDataItem(Utf8JsonWriter writer, IDataItem item, bool includeNullFields, JsonEncodedText? objectName = null)
    {
        if (objectName != null)
        {
            writer.WriteStartObject(objectName.Value);
        }
        else
        {
            writer.WriteStartObject();
        }

        foreach (string fieldName in item.GetFieldNames())
        {
            var fieldValue = item.GetValue(fieldName);
            WriteFieldValue(writer, fieldName, fieldValue, includeNullFields);
        }

        writer.WriteEndObject();
    }

    internal static void WriteFieldValue(Utf8JsonWriter writer, string fieldName, object? fieldValue, bool includeNullFields)
    {
        var propertyName = GetAsUnescaped(fieldName);
        if (fieldValue == null)
        {
            if (includeNullFields)
            {
                writer.WriteNull(propertyName);
            }
        }
        else
        {
            if (fieldValue is IDataItem child)
            {
                WriteDataItem(writer, child, includeNullFields, propertyName);
            }
            else if (fieldValue is not string && fieldValue is IEnumerable children)
            {
                writer.WriteStartArray(propertyName);
                foreach (object? arrayItem in children)
                {
                    if (arrayItem is IDataItem arrayChild)
                    {
                        WriteDataItem(writer, arrayChild, includeNullFields);
                    }
                    else if (TryGetLong(arrayItem, out var longValue)) {
                        writer.WriteNumberValue(longValue);
                    }
                    else if (TryGetULong(arrayItem, out var ulongValue)) {
                        writer.WriteNumberValue(ulongValue);
                    }
                    else if (TryGetInteger(arrayItem, out var intValue)) {
                        writer.WriteNumberValue(intValue);
                    }
                    else if (TryGetUInteger(arrayItem, out var uintValue)) {
                        writer.WriteNumberValue(uintValue);
                    }
                    else if (TryGetNumber(arrayItem, out var number))
                    {
                        writer.WriteNumberValue(number);
                    }
                    else if (arrayItem is bool boolean)
                    {
                        writer.WriteBooleanValue(boolean);
                    }
                    else if (arrayItem is DateTime date)
                    {
                        writer.WriteStringValue(date.ToString("O"));
                    }
                    else if (arrayItem is null)
                    {
                        writer.WriteNullValue();
                    }
                    else
                    {
                        writer.WriteStringValue(GetAsUnescaped(arrayItem.ToString()!));
                    }
                }
                writer.WriteEndArray();
            }
            else if (TryGetLong(fieldValue, out var longValue)) {
                writer.WriteNumber(propertyName, longValue);
            }
            else if (TryGetULong(fieldValue, out var ulongValue)) {
                writer.WriteNumber(propertyName, ulongValue);
            }
            else if (TryGetInteger(fieldValue, out var intValue)) {
                writer.WriteNumber(propertyName, intValue);
            }
            else if (TryGetUInteger(fieldValue, out var uintValue)) {
                writer.WriteNumber(propertyName, uintValue);
            }
            else if (TryGetNumber(fieldValue, out var number))
            {
                writer.WriteNumber(propertyName, number);
            }
            else if (fieldValue is bool boolean)
            {
                writer.WriteBoolean(propertyName, boolean);
            }
            else if (fieldValue is DateTime date)
            {
                writer.WriteString(propertyName, date.ToString("O"));
            }
            else
            {
                writer.WriteString(propertyName, GetAsUnescaped(fieldValue.ToString()!));
            }
        }
    }

    private static JsonEncodedText GetAsUnescaped(string text)
    {
        return JsonEncodedText.Encode(text, JavaScriptEncoder.UnsafeRelaxedJsonEscaping);
    }

    internal static bool TryGetLong(object? x, out long number) 
    {
        if (x is long l) {
            number = l;
            return true;
        }
        number = default;
        return false;
    }

    internal static bool TryGetULong(object? x, out ulong number) 
    {
        if (x is ulong l) {
            number = l;
            return true;
        }
        number = default;
        return false;
    }

    internal static bool TryGetInteger(object? x, out int number) {
        if (x is sbyte b) {
            number = b;
            return true;
        }
        if (x is short s) {
            number = s;
            return true;
        }
        if (x is int l) {
            number = l;
            return true;
        }
        number = default;
        return false;
    }

    internal static bool TryGetUInteger(object? x, out uint number) {
        if (x is byte b) {
            number = b;
            return true;
        }
        if (x is ushort s) {
            number = s;
            return true;
        }
        if (x is uint l) {
            number = l;
            return true;
        }
        number = default;
        return false;
    }

    private static bool TryGetNumber(object? x, out double number)
    {
        if (x is float f)
        {
            number = f;
            return true;
        }
        if (x is double d)
        {
            number = d;
            return true;
        }
        if (x is decimal m)
        {
            number = (double)m;
            return true;
        }
        number = default;
        return false;
    }
}