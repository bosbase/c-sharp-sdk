using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bosbase.Utils;

public static class Serialization
{
    public static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string ToJson(object? value)
    {
        return JsonSerializer.Serialize(value, DefaultJsonOptions);
    }

    public static object? ToSerializable(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case JsonElement elem:
                return FromJsonElement(elem);
            case IDictionary dict:
                var obj = new Dictionary<string, object?>();
                foreach (DictionaryEntry entry in dict)
                {
                    var key = entry.Key?.ToString() ?? string.Empty;
                    var val = ToSerializable(entry.Value);
                    if (val != null)
                    {
                        obj[key] = val;
                    }
                }
                return obj;
            case IEnumerable enumerable when value is not string:
                var list = new List<object?>();
                foreach (var item in enumerable)
                {
                    list.Add(ToSerializable(item));
                }
                return list;
            default:
                return value;
        }
    }

    public static object? FromJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                if (element.TryGetInt64(out var l)) return l;
                if (element.TryGetDouble(out var d)) return d;
                return element.GetRawText();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Array:
                var list = new List<object?>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(FromJsonElement(item));
                }
                return list;
            case JsonValueKind.Object:
                var dict = new Dictionary<string, object?>();
                foreach (var prop in element.EnumerateObject())
                {
                    dict[prop.Name] = FromJsonElement(prop.Value);
                }
                return dict;
            default:
                return element.GetRawText();
        }
    }
}
