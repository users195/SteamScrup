using System.Text;

namespace SteamScrup.Core;

/// <summary>A node in a Valve Data Format (VDF) document.</summary>
public sealed class VdfNode
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, VdfNode> _children = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _order = new();

    public VdfNode(string? key = null) => Key = key;

    public string? Key { get; }

    public IReadOnlyList<string> Keys => _order;

    public IReadOnlyDictionary<string, string> Values => _values;

    public IReadOnlyDictionary<string, VdfNode> Children => _children;

    public void SetValue(string key, string value)
    {
        if (!_values.ContainsKey(key)) _order.Add(key);
        _values[key] = value;
    }

    public void AddChild(VdfNode child)
    {
        var key = child.Key ?? string.Empty;
        if (!_children.ContainsKey(key)) _order.Add(key);
        _children[key] = child;
    }

    public string? GetString(string key) => _values.TryGetValue(key, out var v) ? v : null;

    public VdfNode? GetChild(string key) => _children.TryGetValue(key, out var v) ? v : null;

    public int? GetInt(string key) =>
        int.TryParse(GetString(key), out var i) ? i : null;

    public long? GetLong(string key) =>
        long.TryParse(GetString(key), out var l) ? l : null;

    /// <summary>Depth-first enumeration of every descendant node.</summary>
    public IEnumerable<VdfNode> Descendants()
    {
        foreach (var child in _children.Values)
        {
            yield return child;
            foreach (var grand in child.Descendants()) yield return grand;
        }
    }
}

/// <summary>
/// A small, dependency-free tokenizer/parser for Valve's VDF text format.
/// Handles quoted keys/values, escaped quotes and backslashes, nested braces,
/// "//" line comments and the "key" { ... } object form.
/// </summary>
public static class VdfParser
{
    public static VdfNode Parse(string text)
    {
        var root = new VdfNode();
        var pos = 0;
        ParseInto(text, ref pos, root, depth: 0);
        return root;
    }

    public static VdfNode ParseFile(string path)
    {
        // Steam writes these files as UTF-8 without BOM; fall back to the
        // system default if a BOM-less legacy file trips detection.
        var bytes = File.ReadAllBytes(path);
        var text = DecodeText(bytes);
        return Parse(text);
    }

    public static string DecodeText(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        try
        {
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Default.GetString(bytes);
        }
    }

    private const int MaxDepth = 64;

    private static void ParseInto(string s, ref int pos, VdfNode parent, int depth)
    {
        if (depth > MaxDepth) return;

        while (pos < s.Length)
        {
            SkipWhitespaceAndComments(s, ref pos);
            if (pos >= s.Length) return;

            if (s[pos] == '}')
            {
                pos++;
                return;
            }

            if (s[pos] == '{')
            {
                // Unnamed object body: treat as a child of the current parent.
                pos++;
                ParseInto(s, ref pos, parent, depth + 1);
                continue;
            }

            var key = ReadToken(s, ref pos);
            if (key is null) return;

            SkipWhitespaceAndComments(s, ref pos);
            if (pos >= s.Length) return;

            if (s[pos] == '{')
            {
                pos++;
                var child = new VdfNode(key);
                ParseInto(s, ref pos, child, depth + 1);
                parent.AddChild(child);
            }
            else
            {
                var value = ReadToken(s, ref pos);
                if (value is null) return;
                parent.SetValue(key, value);
            }
        }
    }

    private static void SkipWhitespaceAndComments(string s, ref int pos)
    {
        while (pos < s.Length)
        {
            var c = s[pos];
            if (char.IsWhiteSpace(c))
            {
                pos++;
            }
            else if (c == '/' && pos + 1 < s.Length && s[pos + 1] == '/')
            {
                while (pos < s.Length && s[pos] != '\n') pos++;
            }
            else
            {
                break;
            }
        }
    }

    private static string? ReadToken(string s, ref int pos)
    {
        if (pos >= s.Length) return null;

        if (s[pos] == '"')
        {
            pos++;
            var sb = new StringBuilder();
            while (pos < s.Length)
            {
                var c = s[pos];
                if (c == '\\' && pos + 1 < s.Length)
                {
                    var next = s[pos + 1];
                    sb.Append(next switch
                    {
                        'n' => '\n',
                        't' => '\t',
                        'r' => '\r',
                        _ => next,
                    });
                    pos += 2;
                    continue;
                }

                if (c == '"')
                {
                    pos++;
                    return sb.ToString();
                }

                sb.Append(c);
                pos++;
            }

            return sb.ToString();
        }

        // Unquoted token: read until whitespace or a structural character.
        var start = pos;
        while (pos < s.Length && !char.IsWhiteSpace(s[pos]) && s[pos] != '{' && s[pos] != '}')
            pos++;

        return pos > start ? s[start..pos] : null;
    }
}
