using System.Text.Json;

namespace LogSeqDBExport.Helpers;

/// <summary>
/// Pragmatic "Transit-like" resolver for references such as ^1, ^>, ^@, ...
///
/// Idea:
/// - Cache certain symbolic strings (notably those starting with "~:")
/// - When encountering a string that starts with '^' (except "^ "), replace
///   it with the corresponding cached value
///
/// This is not a full implementation of the Transit format, but it is usually sufficient for this kind of dump.
/// Note: this class logic has been built using AI (Copilot), and almost not modified then.
/// </summary>
public sealed class TransitLikeResolver
{
    private bool cacheInit = false;
    private readonly List<string> done = [];
    private readonly Dictionary<string, string> _cacheByCode = [];
    private int _nextCacheIndex = 0;

    public void InitCache(JsonElement item)
    {
        if (item.ValueKind is JsonValueKind.Array)
        {
            foreach (var x in item.EnumerateArray())
            {
                InitCache(x);
            }
        }
        else if (item.ValueKind is JsonValueKind.String)
        {
            var s = item.GetString();
            if (IsCacheable(s))
            {
                var code = EncodeCacheCode(_nextCacheIndex++);
                _cacheByCode[code] = s!;
                done.Add(s!);
            }
        }
    }

    public object? Decode(JsonElement element)
    {
        if (!cacheInit)
        {
            InitCache(element);
            cacheInit = true;
        }
        return element.ValueKind switch
        {
            JsonValueKind.Array => DecodeArray(element),
            JsonValueKind.Object => DecodeObject(element),
            JsonValueKind.String => DecodeString(element.GetString()!),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

    private List<object?> DecodeArray(JsonElement element)
    {
        return [.. element.EnumerateArray().Select(item => Decode(item))];
    }

    private Dictionary<string, object?> DecodeObject(JsonElement element)
    {
        return element.EnumerateObject().ToDictionary(prop => prop.Name, prop => Decode(prop.Value));
    }

    private string? DecodeString(string s)
    {
        // Special marker, leave it as-is
        if (s == "^ ")
            return s;

        // Cache reference: ^1, ^2, ^>, ^@, ...
        if (IsReferenceCode(s))
        {
            if (_cacheByCode.TryGetValue(s, out var resolved))
            {
                //Console.WriteLine($"{s} is a ref code, resolved to {resolved}");
                return resolved;
            }

            Console.WriteLine($"{s} is a ref code, but wasn't resolved!");
            // If a lookup is missing, keep the raw value to avoid breaking parsing
            return s;
        }

        // // Cacheable value (disabled here — caching is done during InitCache)
        // if (IsCacheable(s))
        // {
        //     var code = EncodeCacheCode(_nextCacheIndex++);
        //     Console.WriteLine($"Caching '{s}' as {code}");

        //     _cacheByCode[code] = s;
        //     //done.Add(s);
        // }

        return s;
    }

    private static bool IsReferenceCode(string s)
    {
        return s.Length == 2 && s[0] == '^' && s != "^ ";
    }

    private bool IsCacheable(string? s)
    {
        return s is not null && (s.StartsWith("~:") || s.StartsWith("~#")); //&& s != "~:keys"
    }

    /// <summary>
    /// Encodage correspondant au pattern observé dans les données :
    /// index 0 -> "^0"
    /// index 1 -> "^1"
    /// ...
    /// index 9 -> "^9"
    /// index 10 -> "^:"
    /// index 11 -> "^;"
    /// index 12 -> "^<"
    /// index 13 -> "^="
    /// index 14 -> "^>"
    /// index 15 -> "^?"
    /// index 16 -> "^@"
    /// index 17 -> "^A"
    /// etc..
    /// </summary>
    private static string EncodeCacheCode(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        // Version simple : un seul caractère, séquence ASCII à partir de '0'
        // 48 => '0', 49 => '1', ... 57 => '9', 58 => ':', ... 64 => '@', 65 => 'A', ...
        var c = (char)('0' + index);

        return "^" + c;
    }
}