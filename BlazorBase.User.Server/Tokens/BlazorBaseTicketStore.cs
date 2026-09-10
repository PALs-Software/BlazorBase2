using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace BlazorBase.User.Server.Tokens;

/// <summary>
/// Default, per-process <see cref="ITicketStore"/> for the Identity application cookie, backed by a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>. Once wired into
/// <see cref="Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions.SessionStore"/>
/// by <see cref="BlazorBaseAuthTokenTransportExtensions.AddBlazorBaseCookieAuthTokens"/>, the cookie
/// itself carries only the key <see cref="StoreAsync"/> hands back — removing that key from here is
/// what makes a cookie value replayed after sign-out fail immediately, instead of staying valid until
/// its own expiry.
/// </summary>
/// <remarks>
/// Two consequences follow from keeping sessions here rather than inside the cookie: a process
/// restart forgets every session — the cookie's key then points at nothing, so the caller is signed
/// out — and a multi-instance host must register a shared <see cref="ITicketStore"/> of its own,
/// since a session established on one node is otherwise unknown to the others. Both are inherent to
/// server-side session storage, not a shortcoming of this particular store, and both are why it is
/// registered with <c>TryAddSingleton</c> rather than hard-coded: a host that needs either property to
/// hold registers its own distributed implementation before calling
/// <see cref="BlazorBaseAuthTokenTransportExtensions.AddBlazorBaseCookieAuthTokens"/>.
/// </remarks>
public sealed class BlazorBaseTicketStore(TimeProvider timeProvider) : ITicketStore
{
    private readonly record struct StoredTicket(AuthenticationTicket Ticket, DateTimeOffset? ExpiresUtc);

    private readonly TimeProvider TimeProvider = timeProvider;
    private readonly ConcurrentDictionary<string, StoredTicket> Tickets = new(StringComparer.Ordinal);

    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        PruneExpiredEntries();

        var key = Guid.NewGuid().ToString("N");
        Tickets[key] = new StoredTicket(ticket, ticket.Properties.ExpiresUtc);

        return Task.FromResult(key);
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        Tickets[key] = new StoredTicket(ticket, ticket.Properties.ExpiresUtc);

        return Task.CompletedTask;
    }

    public Task<AuthenticationTicket?> RetrieveAsync(string key) =>
        Task.FromResult(Tickets.TryGetValue(key, out var stored) ? stored.Ticket : null);

    public Task RemoveAsync(string key)
    {
        Tickets.TryRemove(key, out _);

        return Task.CompletedTask;
    }

    private void PruneExpiredEntries()
    {
        var now = TimeProvider.GetUtcNow();

        foreach (var (key, stored) in Tickets)
        {
            if (stored.ExpiresUtc is { } expiresUtc && expiresUtc < now)
                Tickets.TryRemove(key, out _);
        }
    }
}
