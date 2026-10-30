using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace BlazorBase.Components.HeaderActions;

/// <summary>
/// Renders the header actions as a row of plain icon buttons in their order. The items come from
/// <see cref="IHeaderActionService"/> and <see cref="Items"/>; an item is shown when its visibility predicate, role
/// and policy allow it. With <see cref="MaxInline"/> the surplus items move into an overflow menu behind a "more"
/// button. <c>BaseLayout</c> renders it; it needs no services except the optional <see cref="IHeaderActionService"/>.
/// </summary>
public partial class BaseHeaderActions(
    IServiceProvider serviceProvider,
    NavigationManager navigationManager,
    IStringLocalizer<BaseHeaderActions> localizer) : ComponentBase, IDisposable
{
    #region Injects

    private readonly IServiceProvider ServiceProvider = serviceProvider;
    private readonly NavigationManager NavigationManager = navigationManager;
    private readonly IStringLocalizer<BaseHeaderActions> Localizer = localizer;

    #endregion

    /// <summary>Items in addition to the registered ones; an item with the same id replaces the registered one.</summary>
    [Parameter]
    public IReadOnlyList<HeaderActionItem>? Items { get; set; }

    /// <summary>The most buttons shown directly; the rest go into the overflow menu. Null shows all of them.</summary>
    [Parameter]
    public int? MaxInline { get; set; }

    #region Members

    private const string MenuId = "base-header-actions-menu";

    private IHeaderActionService? Service;
    private AuthenticationStateProvider? AuthenticationProvider;
    private IReadOnlyList<HeaderActionItem> Inline = [];
    private IReadOnlyList<HeaderActionItem> Overflow = [];
    private bool IsMenuOpen;
    private bool IsDisposed;

    #endregion

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Service = ServiceProvider.GetService<IHeaderActionService>();
        AuthenticationProvider = ServiceProvider.GetService<AuthenticationStateProvider>();
        if (Service is not null)
            Service.Changed += OnChanged;

        if (AuthenticationProvider is not null)
            AuthenticationProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;

        NavigationManager.LocationChanged += OnLocationChanged;
    }

    /// <inheritdoc />
    protected override Task OnParametersSetAsync() => EvaluateAsync();

    private async Task EvaluateAsync()
    {
        var items = HeaderActionOrdering.Merge(Service?.Items ?? [], Items);
        ClaimsPrincipal? user = null;
        if (AuthenticationProvider is not null)
            user = (await AuthenticationProvider.GetAuthenticationStateAsync()).User;

        var visible = await HeaderActionOrdering.FilterAsync(items, ServiceProvider, user, ServiceProvider.GetService<IAuthorizationService>());
        if (IsDisposed)
            return;

        var limit = MaxInline is { } max && visible.Count > max ? Math.Max(0, max) : visible.Count;
        Inline = visible.Take(limit).ToList();
        Overflow = visible.Skip(limit).ToList();
        if (Overflow.Count == 0)
            IsMenuOpen = false;
    }

    private string? PressedValue(HeaderActionItem item) =>
        item.IsActive is null ? null : item.IsActive(ServiceProvider) ? "true" : "false";

    private void ToggleMenu() => IsMenuOpen = !IsMenuOpen;

    private void CloseMenu() => IsMenuOpen = false;

    private void OnKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs args)
    {
        if (args.Key == "Escape")
            IsMenuOpen = false;
    }

    private async Task ActivateAsync(HeaderActionItem item)
    {
        IsMenuOpen = false;
        await item.OnInvoke(ServiceProvider);
        await EvaluateAsync();
    }

    private void OnChanged() => _ = InvokeAsync(RefreshAsync);

    private void OnAuthenticationStateChanged(Task<AuthenticationState> state) => _ = InvokeAsync(RefreshAsync);

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args) => _ = InvokeAsync(RefreshAsync);

    private async Task RefreshAsync()
    {
        if (IsDisposed)
            return;

        await EvaluateAsync();
        StateHasChanged();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        IsDisposed = true;
        if (Service is not null)
            Service.Changed -= OnChanged;

        if (AuthenticationProvider is not null)
            AuthenticationProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;

        NavigationManager.LocationChanged -= OnLocationChanged;
    }
}
