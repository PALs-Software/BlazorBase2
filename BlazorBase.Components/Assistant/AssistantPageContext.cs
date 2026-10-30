namespace BlazorBase.Components.Assistant;

/// <summary>
/// What the assistant is told about the page the user is on. Technical hints and ids only — never field values —
/// and untrusted: whoever receives it treats it as data, not as authority.
/// </summary>
public sealed record AssistantPageContext
{
    /// <summary>The key of the website.</summary>
    public string? SiteId { get; init; }

    /// <summary>The path of the page, without query string.</summary>
    public string? Route { get; init; }

    /// <summary>The title of the page.</summary>
    public string? PageTitle { get; init; }

    /// <summary>The UI culture, for example <c>de-DE</c>.</summary>
    public string? Locale { get; init; }

    /// <summary>The names of the roles of the signed-in user.</summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>The type of the entity the page shows, for example a model name.</summary>
    public string? EntityType { get; init; }

    /// <summary>The key of the selected or open record.</summary>
    public string? EntityKey { get; init; }

    /// <summary>A short description of the active filters, for example the count.</summary>
    public string? Filters { get; init; }

    /// <summary>The id of the active list preset.</summary>
    public string? Preset { get; init; }

    /// <summary>The error text currently visible on the page.</summary>
    public string? ErrorText { get; init; }

    /// <summary>Further key/value hints, contributed by components.</summary>
    public IReadOnlyDictionary<string, string> Hints { get; init; } = new Dictionary<string, string>();

    /// <summary>True when values were shortened or dropped to fit <see cref="AssistantClientOptions.MaxContextBytes"/>.</summary>
    public bool IsTruncated { get; init; }
}
