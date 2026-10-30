using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BlazorBase.Components.Assistant;

/// <summary>
/// Collects the <see cref="AssistantPageContext"/> that goes along with a message: route, culture, roles, and what
/// components contribute through <see cref="IAssistantContextProvider"/> or <see cref="AssistantContextHint"/>.
/// </summary>
/// <remarks>
/// Only ids and technical hints belong into the context, never field values. The serialized context never exceeds
/// <see cref="AssistantClientOptions.MaxContextBytes"/>: texts are shortened first, then hints, then the other
/// values are dropped, and <see cref="AssistantPageContext.IsTruncated"/> says so.
/// </remarks>
public sealed class AssistantContextService(
    NavigationManager navigationManager,
    IOptions<AssistantClientOptions> options,
    IServiceProvider serviceProvider)
{
    #region Injects

    private readonly NavigationManager NavigationManager = navigationManager;
    private readonly IOptions<AssistantClientOptions> Options = options;
    private readonly IServiceProvider ServiceProvider = serviceProvider;

    #endregion

    #region Members

    private const int MaxRoles = 20;
    private const int MaxRoleLength = 64;
    private const int MaxHints = 20;
    private const int MaxHintKeyLength = 60;
    private const int MaxHintValueLength = 200;
    private const int MaxRouteLength = 256;
    private const int MaxTitleLength = 200;
    private const int MaxIdLength = 100;
    private const int MaxFilterLength = 300;
    private const int MaxErrorLength = 500;

    private readonly List<IAssistantContextProvider> Providers = [];
    private readonly Dictionary<string, string> Hints = new(StringComparer.Ordinal);

    #endregion

    /// <summary>The title of the page; the panel falls back to the document title when it is null.</summary>
    public string? PageTitle { get; set; }

    /// <summary>The error text currently visible on the page.</summary>
    public string? VisibleError { get; set; }

    /// <summary>Registers a provider until the returned handle is disposed.</summary>
    public IDisposable Register(IAssistantContextProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        Providers.Add(provider);
        return new Registration(() => Providers.Remove(provider));
    }

    /// <summary>Sets a hint that stays until it is removed; a null or empty value removes it.</summary>
    public void SetHint(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        if (string.IsNullOrEmpty(value))
            Hints.Remove(key.Trim());
        else
            Hints[key.Trim()] = value;
    }

    /// <summary>Removes a hint set with <see cref="SetHint"/>.</summary>
    public void RemoveHint(string key) => SetHint(key, null);

    /// <summary>Builds the context for the current page.</summary>
    /// <param name="documentTitle">The title of the browser document, used when <see cref="PageTitle"/> is not set.</param>
    /// <param name="cancellationToken">Cancels the capture.</param>
    public async Task<AssistantPageContext> CaptureAsync(string? documentTitle = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var builder = new AssistantContextBuilder { ErrorText = VisibleError };

        foreach (var (key, value) in Hints)
            builder.AddHint(key, value);

        foreach (var provider in Providers.ToArray())
            Contribute(provider, builder);

        var context = new AssistantPageContext
        {
            SiteId = Options.Value.SiteId,
            Route = CurrentRoute(),
            PageTitle = string.IsNullOrWhiteSpace(PageTitle) ? documentTitle : PageTitle,
            Locale = CultureInfo.CurrentUICulture.Name,
            Roles = await ReadRolesAsync(),
            EntityType = builder.EntityType,
            EntityKey = builder.EntityKey,
            Filters = builder.Filters,
            Preset = builder.Preset,
            ErrorText = builder.ErrorText,
            Hints = builder.Hints
        };

        return Limit(context, Options.Value.MaxContextBytes);
    }

    internal static AssistantPageContext Limit(AssistantPageContext context, int maxBytes)
    {
        var truncated = false;
        var current = Shorten(context, ref truncated);

        if (SerializedSize(current) <= maxBytes)
            return current with { IsTruncated = truncated };

        current = current with { IsTruncated = true };

        foreach (var key in current.Hints.Keys.OrderByDescending(key => key, StringComparer.Ordinal))
        {
            var remaining = current.Hints.Where(pair => pair.Key != key).ToDictionary(pair => pair.Key, pair => pair.Value);
            current = current with { Hints = remaining };

            if (SerializedSize(current) <= maxBytes)
                return current;
        }

        foreach (var drop in DropSteps)
        {
            current = drop(current);

            if (SerializedSize(current) <= maxBytes)
                return current;
        }

        return current;
    }

    private static readonly Func<AssistantPageContext, AssistantPageContext>[] DropSteps =
    [
        context => context with { Roles = [] },
        context => context with { ErrorText = null },
        context => context with { Filters = null },
        context => context with { Preset = null },
        context => context with { EntityKey = null },
        context => context with { PageTitle = null },
        context => context with { EntityType = null },
        context => context with { Locale = null },
        context => context with { Route = null }
    ];

    private static AssistantPageContext Shorten(AssistantPageContext context, ref bool truncated)
    {
        var changed = false;

        string? Cut(string? value, int max)
        {
            var result = Truncate(value, max);

            if (!ReferenceEquals(result, value))
                changed = true;

            return result;
        }

        var hints = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (key, value) in context.Hints.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (hints.Count >= MaxHints)
            {
                changed = true;
                break;
            }

            hints[Cut(key, MaxHintKeyLength)!] = Cut(value, MaxHintValueLength)!;
        }

        var roles = context.Roles.Take(MaxRoles).Select(role => Cut(role, MaxRoleLength)!).ToArray();
        changed |= roles.Length < context.Roles.Count;

        var shortened = context with
        {
            Route = Cut(context.Route, MaxRouteLength),
            PageTitle = Cut(context.PageTitle, MaxTitleLength),
            EntityType = Cut(context.EntityType, MaxIdLength),
            EntityKey = Cut(context.EntityKey, MaxIdLength),
            Filters = Cut(context.Filters, MaxFilterLength),
            Preset = Cut(context.Preset, MaxIdLength),
            ErrorText = Cut(context.ErrorText, MaxErrorLength),
            Roles = roles,
            Hints = hints
        };

        truncated |= changed;
        return shortened;
    }

    private static string? Truncate(string? value, int max)
    {
        if (value is null || value.Length <= max)
            return value;

        var length = max - 1;

        if (length > 0 && char.IsHighSurrogate(value[length - 1]))
            length--;

        return value[..length] + "…";
    }

    private static int SerializedSize(AssistantPageContext context) =>
        JsonSerializer.SerializeToUtf8Bytes(context, AssistantJson.Options).Length;

    private static void Contribute(IAssistantContextProvider provider, AssistantContextBuilder builder)
    {
        try
        {
            provider.Contribute(builder);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
        }
    }

    private string CurrentRoute()
    {
        var path = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);
        var end = path.IndexOfAny(['?', '#']);
        return "/" + (end < 0 ? path : path[..end]);
    }

    private async Task<IReadOnlyList<string>> ReadRolesAsync()
    {
        var provider = ServiceProvider.GetService<AuthenticationStateProvider>();

        if (provider is null)
            return [];

        try
        {
            var state = await provider.GetAuthenticationStateAsync();
            var identity = state.User.Identity as ClaimsIdentity;
            var roleClaimType = identity?.RoleClaimType ?? ClaimTypes.Role;

            return state.User.Claims
                .Where(claim => claim.Type == roleClaimType || claim.Type == ClaimTypes.Role || claim.Type == "role")
                .Select(claim => claim.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return [];
        }
    }

    private sealed class Registration(Action onDispose) : IDisposable
    {
        private Action? OnDispose = onDispose;

        public void Dispose()
        {
            OnDispose?.Invoke();
            OnDispose = null;
        }
    }
}
