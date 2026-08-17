using Microsoft.AspNetCore.Components;

namespace BlazorBase.User.Components;

public partial class RedirectToLogin(NavigationManager navigation)
{
    #region Injects
    private readonly NavigationManager Navigation = navigation;
    #endregion

    protected override void OnInitialized()
    {
        var relativePath = Navigation.ToBaseRelativePath(Navigation.Uri);

        if (relativePath.StartsWith("login", StringComparison.OrdinalIgnoreCase))
        {
            Navigation.NavigateTo("/login", forceLoad: false);
            return;
        }

        var encoded = Uri.EscapeDataString("/" + relativePath);
        Navigation.NavigateTo($"/login?returnUrl={encoded}", forceLoad: false);
    }
}
