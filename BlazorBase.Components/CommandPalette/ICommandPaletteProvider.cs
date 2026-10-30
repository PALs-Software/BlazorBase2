using System.Globalization;
using System.Security.Claims;

namespace BlazorBase.Components.CommandPalette;

/// <summary>
/// What a <see cref="ICommandPaletteProvider"/> is asked for.
/// </summary>
public sealed record CommandPaletteRequest
{
    /// <summary>
    /// The text the user typed, trimmed. Empty when the palette has just opened: return the entries to suggest then,
    /// preferably all of them, because the palette ranks, filters and caps the result itself.
    /// </summary>
    public required string Query { get; init; }

    /// <summary>The current user; an anonymous principal when nobody is signed in.</summary>
    public required ClaimsPrincipal User { get; init; }

    /// <summary>The current UI culture.</summary>
    public required CultureInfo Culture { get; init; }

    /// <summary>The total number of results the palette shows; a hint for providers that search a large data set.</summary>
    public int MaxResults { get; init; } = 30;
}

/// <summary>
/// A source of <see cref="CommandPaletteEntry"/> values for the command palette. Register providers with
/// <c>AddCommandPaletteProvider</c>.
/// </summary>
/// <remarks>
/// A provider may be a synchronous in-memory list or a server backed search. One that throws or does not answer
/// within <see cref="CommandPaletteOptions.ProviderTimeout"/> is logged and skipped; the other providers still show.
/// The palette applies the <see cref="CommandPaletteEntry.RequiredRole"/> gate to everything a provider returns, but
/// finer checks (policies, per record permissions) are the provider's job: it must never return an entry the user
/// may not use.
/// </remarks>
public interface ICommandPaletteProvider
{
    /// <summary>Returns the entries for the request; the token is cancelled when the user has typed on.</summary>
    ValueTask<IReadOnlyList<CommandPaletteEntry>> GetEntriesAsync(CommandPaletteRequest request, CancellationToken cancellationToken);
}
