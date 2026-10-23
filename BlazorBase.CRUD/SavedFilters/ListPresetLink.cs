using System.Text;

namespace BlazorBase.CRUD.SavedFilters;

/// <summary>
/// Builds links that open a list with one of its filter presets applied (<c>/Boosts?preset=my-pending</c>). Pure and
/// WASM-safe, so a dashboard tile, a menu entry or a mail can point at the exact filtered list.
/// </summary>
public static class ListPresetLink
{
    /// <summary>
    /// The query-string parameter a list reads; the default of <c>BaseList.PresetParameterName</c>.
    /// </summary>
    public const string DefaultParameterName = "preset";

    /// <summary>
    /// Returns <paramref name="listPath"/> with <c>preset=&lt;presetId&gt;</c> in its query string. An existing query string
    /// is kept, an existing <c>preset</c> parameter is replaced, a fragment stays at the end and the id is URL-encoded.
    /// An empty <paramref name="presetId"/> returns the path unchanged; an empty path is the root.
    /// </summary>
    /// <param name="listPath">The list route, relative or absolute, optionally with a query string or fragment.</param>
    /// <param name="presetId">The <c>FilterPreset.Id</c> to apply.</param>
    public static string Build(string listPath, string presetId)
    {
        ArgumentNullException.ThrowIfNull(listPath);

        var path = listPath.Length == 0 ? "/" : listPath;

        if (String.IsNullOrEmpty(presetId))
            return path;

        var fragment = String.Empty;
        var fragmentStart = path.IndexOf('#');

        if (fragmentStart >= 0)
        {
            fragment = path[fragmentStart..];
            path = path[..fragmentStart];
        }

        var query = String.Empty;
        var queryStart = path.IndexOf('?');

        if (queryStart >= 0)
        {
            query = path[(queryStart + 1)..];
            path = path[..queryStart];
        }

        var builder = new StringBuilder(path).Append('?');

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!IsPresetPair(pair))
                builder.Append(pair).Append('&');
        }

        return builder.Append(DefaultParameterName).Append('=').Append(Uri.EscapeDataString(presetId)).Append(fragment).ToString();
    }

    private static bool IsPresetPair(string pair)
    {
        var separator = pair.IndexOf('=');
        var key = separator < 0 ? pair : pair[..separator];

        return String.Equals(Uri.UnescapeDataString(key), DefaultParameterName, StringComparison.OrdinalIgnoreCase);
    }
}
