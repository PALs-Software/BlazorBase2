using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Core;
using BlazorBase.User.Models;
using BlazorBase.User.Pages;
using BlazorBase.User.Services;
using BlazorBase.User.Test.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
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

    private IRenderedComponent<IComponent> OpenAddDialog()
    {
        SetupServices();
        var cut = Render(ViewWithDialogProvider);
        cut.Find(".base-list-add").Click();
        cut.WaitForElement(".base-card-save", TimeSpan.FromSeconds(2));
        return cut;
    }

    private static DialogOptions OptionsOf(IRenderedComponent<IComponent> cut) =>
        cut.FindComponent<BaseDialog<UserModel>>().Instance.DialogInstance.Options;

    [Fact]
    public void AddDialog_HasSingleSaveAndCancel_NoDuplicateFooter()
    {
        var cut = OpenAddDialog();

        Assert.Single(cut.FindAll(".base-card-save"));
        Assert.Single(cut.FindAll(".base-card-cancel"));
        Assert.False(OptionsOf(cut).Footer.PrimaryAction.Visible);
        Assert.False(OptionsOf(cut).Footer.SecondaryAction.Visible);
    }

    /// <summary>
    /// The Fluent dismiss cross is switched off on purpose: it closes the dialog without going
    /// through the card, so a half-filled form would vanish with no way back.
    /// </summary>
    [Fact]
    public void AddDialog_RendersNoDismissCross()
    {
        var cut = OpenAddDialog();

        Assert.False(OptionsOf(cut).Header.CloseAction.Visible);
    }

    /// <summary>
    /// Likewise for the Escape key, which would otherwise close the dialog behind the card's back: the
    /// dialog stays open so an accidental key press costs nothing.
    /// </summary>
    [Fact]
    public void AddDialog_IgnoresEscape()
    {
        var cut = OpenAddDialog();

        Assert.True(OptionsOf(cut).PreventDismissOnEscape);
        Assert.Single(cut.FindComponents<BaseDialog<UserModel>>());
    }

    /// <summary>
    /// Cancel is therefore the one way out, and it has to work.
    /// </summary>
    [Fact]
    public void ClickingCancel_ClosesDialog()
    {
        var cut = OpenAddDialog();

        cut.Find(".base-card-actions .base-card-cancel").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindComponents<BaseDialog<UserModel>>()), TimeSpan.FromSeconds(2));
    }
}
