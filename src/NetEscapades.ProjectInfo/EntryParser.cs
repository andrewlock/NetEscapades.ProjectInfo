using System.Buffers;
using System.Text;

namespace NetEscapades.ProjectInfo;

/// <summary>
/// Parses the ProjectInfoItems property, a comma-separated list of "S|name|value" or "U|name|value" entries
/// in which the name and value are URI-escaped (see the .targets file). Works over spans to avoid intermediate strings.
/// </summary>
internal static class EntryParser
{
    public static EquatableArray<ProjectInfoEntry> Parse(string? raw)
    {
        if (raw is null)
        {
            return default;
        }

        // Escaped text never contains ',', so the number of commas gives the maximum number of entries
        var capacity = 1;
        foreach (var c in raw)
        {
            if (c == ',')
            {
                capacity++;
            }
        }

        var entries = new ProjectInfoEntry[capacity];
        var count = 0;
        var remaining = raw.AsSpan();
        while (!remaining.IsEmpty)
        {
            ReadOnlySpan<char> item;
            var comma = remaining.IndexOf(',');
            if (comma < 0)
            {
                item = remaining;
                remaining = default;
            }
            else
            {
                item = remaining.Slice(0, comma);
                remaining = remaining.Slice(comma + 1);
            }

            if (!item.IsEmpty)
            {
                entries[count++] = ParseEntry(item);
            }
        }

        if (count != entries.Length)
        {
            Array.Resize(ref entries, count);
        }

        return new EquatableArray<ProjectInfoEntry>(entries);
    }

    private static ProjectInfoEntry ParseEntry(ReadOnlySpan<char> item)
    {
        if (item.Length >= 3 && item[1] == '|' && (item[0] == 'S' || item[0] == 'U'))
        {
            var rest = item.Slice(2);
            var separator = rest.IndexOf('|');
            if (separator >= 0)
            {
                var name = rest.Slice(0, separator);
                var value = rest.Slice(separator + 1);

                // Correctly escaped text never contains '|' or '$'. A '$' means MSBuild didn't evaluate the
                // expression, because the value contained a back-quote
                if (value.IndexOf('|') < 0 && name.IndexOf('$') < 0 && value.IndexOf('$') < 0)
                {
                    return new ProjectInfoEntry(
                        item[0] == 'S' ? ProjectInfoKind.String : ProjectInfoKind.Utf8,
                        Unescape(name),
                        Unescape(value));
                }
            }
        }

        return new ProjectInfoEntry(ProjectInfoKind.String, string.Empty, null);
    }

    /// <summary>
    /// Reverses <c>Uri.EscapeDataString</c>. The escaped text is always ASCII, with non-ASCII characters
    /// encoded as percent-escaped UTF-8 bytes. Only allocates the result string when nothing needs unescaping,
    /// otherwise additionally borrows a pooled buffer.
    /// </summary>
    private static string Unescape(ReadOnlySpan<char> escaped)
    {
        if (escaped.IndexOf('%') < 0)
        {
            return escaped.ToString();
        }

        var buffer = ArrayPool<byte>.Shared.Rent(escaped.Length);
        try
        {
            var length = 0;
            for (var i = 0; i < escaped.Length; i++)
            {
                var c = escaped[i];
                if (c == '%' && i + 2 < escaped.Length &&TryReadHex(escaped[i + 1], escaped[i + 2], out var b))
                {
                    buffer[length++] = b;
                    i += 2;
                }
                else
                {
                    buffer[length++] = (byte)c;
                }
            }

            return Encoding.UTF8.GetString(buffer, 0, length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static bool TryReadHex(char high, char low, out byte value)
    {
        var h = HexValue(high);
        var l = HexValue(low);
        value = (byte)((h << 4) | l);
        return (h | l) >= 0;
    }

    private static int HexValue(char c)
        => c is >= '0' and <= '9' ? c - '0'
         : c is >= 'A' and <= 'F' ? c - 'A' + 10
         : c is >= 'a' and <= 'f' ? c - 'a' + 10
         : -1;
}
