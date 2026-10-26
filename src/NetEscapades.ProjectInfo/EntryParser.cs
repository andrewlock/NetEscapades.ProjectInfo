using System.Text;

namespace NetEscapades.ProjectInfo;

/// <summary>
/// Parses the ProjectInfoItems property, a comma-separated list of "S|name|value" or "U|name|value" entries
/// in which the name and value are base64-encoded UTF-8 (see the .targets file). Works over spans to avoid intermediate strings.
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

                // Correctly encoded text is always valid base64, so never contains '|' or '$'. A '$' means MSBuild
                // didn't evaluate the expression, because the value contained a back-quote
                if (value.IndexOf('|') < 0
                    && TryDecode(name, out var decodedName)
                    && TryDecode(value, out var decodedValue))
                {
                    return new ProjectInfoEntry(
                        item[0] == 'S' ? ProjectInfoKind.String : ProjectInfoKind.Utf8,
                        decodedName,
                        decodedValue);
                }
            }
        }

        return new ProjectInfoEntry(ProjectInfoKind.String, string.Empty, null);
    }

    /// <summary>
    /// Reverses <c>[MSBuild]::ConvertToBase64</c>: base64 of the UTF-8 bytes. Returns <c>false</c> for anything that
    /// isn't valid base64. This only runs when the raw property changes, so the intermediate allocations are acceptable.
    /// </summary>
    private static bool TryDecode(ReadOnlySpan<char> encoded, out string decoded)
    {
        try
        {
            var bytes = Convert.FromBase64String(encoded.ToString());
            decoded = Encoding.UTF8.GetString(bytes);
            return true;
        }
        catch (FormatException)
        {
            decoded = string.Empty;
            return false;
        }
    }
}
