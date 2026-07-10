using System.Collections;
using System.Globalization;
using System.Text.Json;

namespace NacosSyncTool.Windows.Models;

public static class ConfigValueFormatter
{
    private static readonly JsonSerializerOptions DisplayJsonOptions = new()
    {
        WriteIndented = true
    };

    public static string ToDisplayText(object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is string text)
        {
            return text;
        }

        if (IsScalar(value))
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        try
        {
            return JsonSerializer.Serialize(NormalizeForDisplay(value), DisplayJsonOptions);
        }
        catch
        {
            return value.ToString() ?? string.Empty;
        }
    }

    private static object? NormalizeForDisplay(object? value)
    {
        if (value is null || value is string || IsScalar(value))
        {
            return value;
        }

        if (value is JsonElement jsonElement)
        {
            return NormalizeJsonElement(jsonElement);
        }

        if (value is IDictionary dictionary)
        {
            var result = new Dictionary<string, object?>();
            foreach (DictionaryEntry entry in dictionary)
            {
                result[Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty] =
                    NormalizeForDisplay(entry.Value);
            }
            return result;
        }

        if (value is IEnumerable enumerable)
        {
            return enumerable.Cast<object?>().Select(NormalizeForDisplay).ToList();
        }

        return value;
    }

    private static object? NormalizeJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(property => property.Name, property => NormalizeJsonElement(property.Value)),
            JsonValueKind.Array => element.EnumerateArray().Select(NormalizeJsonElement).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var longValue)
                ? longValue
                : element.TryGetDouble(out var doubleValue) ? doubleValue : element.GetRawText(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

    private static bool IsScalar(object value)
    {
        var type = value.GetType();
        return type.IsPrimitive
               || value is decimal
               || value is DateTime
               || value is DateTimeOffset
               || value is Guid
               || value is Enum;
    }
}
