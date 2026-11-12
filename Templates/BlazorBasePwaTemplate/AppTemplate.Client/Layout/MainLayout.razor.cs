using System.Security.Claims;
using AppTemplate.Shared.Modules.Authentication;
using BlazorBase.Components.Layout.Navigation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components.Icons.Regular;

namespace AppTemplate.Client.Layout;

public partial class MainLayout(
    AuthenticationStateProvider authenticationStateProvider,
    IStringLocalizer<MainLayout> localizer) : LayoutComponentBase, IDisposable
{
    #region Injects

    private readonly AuthenticationStateProvider AuthenticationStateProvider = authenticationStateProvider;
    private readonly IStringLocalizer<MainLayout> Localizer = localizer;

    #endregion

    private IReadOnlyList<NavigationItem> NavigationItems = [];

    private bool IsAuthenticated;

    private string UserInitials = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        AuthenticationStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;

        await ReadAuthenticationStateAsync();
    }

    private async Task ReadAuthenticationStateAsync()
    {
        var authenticationState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = authenticationState.User;

        IsAuthenticated = user.Identity?.IsAuthenticated == true;
        NavigationItems = BuildNavigationItems();
        UserInitials = ResolveUserInitials(user);
    }

    /// <summary>
    /// One grouped tree for all presentations — the rail (with the group headings as sections), the mobile
    /// bar (<c>IsMobilePrimary</c>) and the command palette render it, and <c>NavigationVisibility</c> drops
    /// what the current user may not see (a group without visible children disappears as well), so they
    /// cannot diverge. Keep the groups: a flat list leaves the sidebar without section headings.
    /// </summary>
    private IReadOnlyList<NavigationItem> BuildNavigationItems() =>
    [
        WorkspaceGroup(),
        AdministrationGroup(),
    ];

    private NavigationItem WorkspaceGroup() => new()
    {
        Key = "workspace",
        Label = Localizer["NavGroupWorkspace"],
        Icon = new Size20.Briefcase(),
        ExpandedByDefault = true,
        Children =
        [
            new()
            {
                Href = string.Empty,
                Label = Localizer["NavHome"],
                MatchAll = true,
                IsMobilePrimary = true,
                Icon = new Size20.Home(),
            },
            new()
            {
                Href = "notes",
                Label = Localizer["NavNotes"],
                IsMobilePrimary = true,
                Icon = new Size20.Notepad(),
            },
        ],
    };

    private NavigationItem AdministrationGroup() => new()
    {
        Key = "administration",
        Label = Localizer["NavGroupAdministration"],
        RequiredRole = RoleConstants.Admin,
        Icon = new Size20.ShieldPerson(),
        ExpandedByDefault = true,
        Children =
        [
            new()
            {
                Href = "admin/users",
                Label = Localizer["NavUsers"],
                RequiredRole = RoleConstants.Admin,
                IsMobilePrimary = true,
                Icon = new Size20.PeopleSettings(),
            },
        ],
    };

    private static string ResolveUserInitials(ClaimsPrincipal user)
    {
        var displayName = user.FindFirst("displayName")?.Value;

        if (string.IsNullOrWhiteSpace(displayName))
            return "?";

        var parts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length == 1
            ? parts[0][..1].ToUpperInvariant()
            : string.Concat(parts[0][..1], parts[^1][..1]).ToUpperInvariant();
    }

    private void OnAuthenticationStateChanged(Task<AuthenticationState> authenticationStateTask)
        => InvokeAsync(async () =>
        {
            await ReadAuthenticationStateAsync();
            StateHasChanged();
        });

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        AuthenticationStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
    }
}
