namespace Bosbase.Utils;

public static class DictionaryExtensions
{
    public static TValue? GetValueOrDefault<TKey, TValue>(IDictionary<TKey, TValue> dictionary, TKey key)
    {
        if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));
        return dictionary.TryGetValue(key, out var value) ? value : default;
    }

    public static TValue? GetValueOrDefault<TKey, TValue>(IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue)
    {
        if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));
        return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
    }

    public static object? SafeGet(IDictionary<string, object?>? dictionary, string key)
    {
        if (dictionary == null) return null;
        return dictionary.TryGetValue(key, out var value) ? value : null;
    }

    public static object? SafeGet(IDictionary<string, object?>? dictionary, string key, object? defaultValue)
    {
        if (dictionary == null) return defaultValue;
        return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
    }

}
