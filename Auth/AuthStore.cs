using System.Text;
using System.Text.Json;

namespace Bosbase.Auth;

/// <summary>
/// In-memory auth store tracking token and authenticated record data.
/// </summary>
public class AuthStore
{
    private readonly object _lock = new();
    private readonly List<Action<string, IDictionary<string, object?>?>> _listeners = new();
    private string _token = string.Empty;
    private IDictionary<string, object?>? _record;

    public string Token => _token;
    public IDictionary<string, object?>? Record => _record;

    public bool IsValid()
    {
        var token = _token;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        try
        {
            var payload = parts[1];
            // pad base64 segment
            payload += new string('=', (4 - payload.Length % 4) % 4);
            var bytes = Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/'));
            var json = Encoding.UTF8.GetString(bytes);
            var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("exp", out var expElement))
            {
                return false;
            }

            long expSeconds = expElement.ValueKind switch
            {
                JsonValueKind.Number when expElement.TryGetInt64(out var v) => v,
                _ => 0
            };

            return expSeconds > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
        catch
        {
            return false;
        }
    }

    public void AddListener(Action<string, IDictionary<string, object?>?> listener)
    {
        lock (_lock)
        {
            _listeners.Add(listener);
        }
    }

    public void RemoveListener(Action<string, IDictionary<string, object?>?> listener)
    {
        lock (_lock)
        {
            _listeners.Remove(listener);
        }
    }

    public void Save(string token, IDictionary<string, object?>? record)
    {
        List<Action<string, IDictionary<string, object?>?>> listenersCopy;

        lock (_lock)
        {
            _token = token ?? string.Empty;
            _record = record != null ? new Dictionary<string, object?>(record) : null;
            listenersCopy = new List<Action<string, IDictionary<string, object?>?>>(_listeners);
        }

        foreach (var listener in listenersCopy)
        {
            try
            {
                listener(_token, _record);
            }
            catch
            {
                // best-effort notification
            }
        }
    }

    public void Clear() => Save(string.Empty, null);
}
