using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Localization;
using BlazorBase.User.Models;
using BlazorBase.User.Pages;
using BlazorBase.User.Services;
using BlazorBase.User.Test.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
using Xunit;

namespace BlazorBase.User.Test.Components;

/// <summary>
/// Behavioural tests for the CRUD detail dialog opened from <see cref="UserManagementView"/>'s
/// <c>BaseList</c>. The card owns its own Save/Cancel buttons and closes the dialog through its
/// callbacks, so the dialog must not also render the Fluent auto-footer (a duplicate, unwired
/// Save/Cancel pair whose Save bypassed the card and skipped the list reload) and must not offer
/// a second, unwired way out.
/// </summary>
public class UserManagementDialogTests : UserBunitTestContextBase
{
    private void SetupServices()
    {
        Services.AddLocalization();
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("admin-user");
        authContext.SetRoles("Admin");
        Services.AddBlazorBaseUserManagementInputs();
        Services.AddSingleton<IUserRoleProvider>(new StubUserRoleProvider("Admin", "User"));

        var provider = new SeededUserModelDataProvider(
            new UserModel { Id = "1", DisplayName = "Ada", Email = "ada@example.com", Role = "Admin", IsActive = true });
        Services.AddSingleton<IBaseDataProvider<UserModel>>(provider);
    }

    private static readonly RenderFragment ViewWithDialogProvider = builder =>
    {
        builder.OpenComponent<FluentDialogProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<UserManagementView>(1);
        builder.CloseComponent();
    };

    /// <summary>
    /// Resolves the framework captions the same way <c>BaseCard</c> does, so the assertions hold
    /// whichever culture the test run happens to use.
    /// </summary>
    private string FrameworkCaption(string key)
        => Services.GetRequiredService<IStringLocalizerFactory>()
            .Create(typeof(BlazorBaseCrudResources))[key].Value;

    [Fact]
    public void AddDialog_HasSingleSaveAndCancel_NoDuplicateFooter()
    {
        SetupServices();
        var cut = Render(ViewWithDialogProvider);
        cut.Find("fluent-button[appearance='accent']").Click();
        cut.WaitForElement("fluent-dialog", TimeSpan.FromSeconds(2));

        var buttons = cut.FindAll("fluent-button");
        var saveButtons = buttons.Count(button => button.TextContent.Contains(FrameworkCaption("Save")));
        var cancelButtons = buttons.Count(button => button.TextContent.Contains(FrameworkCaption("Cancel")));

        Assert.Equal(1, saveButtons);
        Assert.Equal(1, cancelButtons);
    }

    /// <summary>
    /// The Fluent dismiss cross is switched off on purpose: it closes the dialog without going
    /// through the card, so a half-filled form would vanish with no way back.
    /// </summary>
    [Fact]
    public void AddDialog_RendersNoDismissCross()
    {
        SetupServices();
        var cut = Render(ViewWithDialogProvider);
        cut.Find("fluent-button[appearance='accent']").Click();
        cut.WaitForElement("fluent-dialog", TimeSpan.FromSeconds(2));

        Assert.Empty(cut.FindAll("#dialog_close"));
    }

    /// <summary>
    /// Likewise for the overlay click and the Escape key, both of which reach the component as a
    /// dismiss event: the dialog stays open so an accidental click outside costs nothing.
    /// </summary>
    [Fact]
    public void AddDialog_IgnoresADismissEvent()
    {
        SetupServices();
        var cut = Render(ViewWithDialogProvider);
        cut.Find("fluent-button[appearance='accent']").Click();
        cut.WaitForElement("fluent-dialog", TimeSpan.FromSeconds(2));
        var dialog = cut.Find("fluent-dialog");

        dialog.TriggerEvent("ondialogdismiss", new DialogEventArgs { Id = dialog.Id, Reason = "dismiss" });

        Assert.Single(cut.FindAll("fluent-dialog"));
    }

    /// <summary>
    /// Cancel is therefore the one way out, and it has to work.
    /// </summary>
    [Fact]
    public void ClickingCancel_ClosesDialog()
    {
        SetupServices();
        var cut = Render(ViewWithDialogProvider);
        cut.Find("fluent-button[appearance='accent']").Click();
        cut.WaitForElement("fluent-dialog", TimeSpan.FromSeconds(2));

        var cancelCaption = FrameworkCaption("Cancel");

        cut.FindAll("fluent-button")
            .First(button => button.TextContent.Contains(cancelCaption))
            .Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("fluent-dialog")), TimeSpan.FromSeconds(2));
    }
}
