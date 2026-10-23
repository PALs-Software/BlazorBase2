using BlazorBase.Dashboard.Test.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.Dashboard.Test.Layout;

public sealed class RoleCenterPersonalizationTests : DashboardTestContext
{
    private readonly PartProbe Probe = new();
    private readonly RecordingLayoutStore Store = new();

    public RoleCenterPersonalizationTests() => Services.AddSingleton(Probe);

    private void RegisterThreeParts() =>
        Services.AddBlazorBaseDashboard(options => options
            .AddPart<PartA>("alpha", priority: 10, title: "Alpha")
            .AddPart<PartB>("beta", priority: 20, title: "Beta")
            .AddPart<PartC>("gamma", priority: 30, title: "Gamma"));

    private IRenderedComponent<RoleCenter> RenderWithStore(Action<ComponentParameterCollectionBuilder<RoleCenter>>? configure = null)
    {
        Services.AddSingleton<IDashboardLayoutStore>(Store);
        return Render<RoleCenter>(parameters => configure?.Invoke(parameters));
    }

    private static string[] PartNames(IRenderedComponent<RoleCenter> cut) =>
        [.. cut.FindAll(".named-part").Select(element => element.TextContent)];

    private static string[] CardTitles(IRenderedComponent<RoleCenter> cut) =>
        [.. cut.FindAll(".rc-edit-card-title").Select(element => element.TextContent)];

    private static void Press(IRenderedComponent<RoleCenter> cut, string partId, string action) =>
        cut.Find($"[data-edit-part='{partId}'][data-edit-action='{action}']").Click();

    private static void StartEditing(IRenderedComponent<RoleCenter> cut) => cut.Find(".rc-personalize").Click();

    private static DashboardLayout Layout(params (string Id, bool Hidden)[] parts) => new()
    {
        Parts = [.. parts.Select((item, index) => new DashboardPartPreference { PartId = item.Id, Hidden = item.Hidden, Order = index })]
    };

    private static string Announcement(IRenderedComponent<RoleCenter> cut) =>
        cut.Find(".rc-sr-only").TextContent.Replace('\u00A0', ' ').Trim();

    [Fact]
    public void WithoutAStore_ThereIsNoPersonalizationUi()
    {
        RegisterThreeParts();

        var cut = Render<RoleCenter>();

        Assert.Empty(cut.FindAll(".rc-personalize"));
        Assert.Empty(cut.FindAll(".rc-sr-only"));
        Assert.Equal(["PartA", "PartB", "PartC"], PartNames(cut));
    }

    [Fact]
    public void WithAStore_TheMetaRowOffersPersonalize()
    {
        RegisterThreeParts();

        var cut = RenderWithStore();

        var button = cut.Find(".rc-personalize");
        Assert.Equal("button", button.GetAttribute("type"));
        Assert.Contains("Personalize", button.TextContent);
        Assert.Empty(cut.FindAll(".rc-edit-bar"));
    }

    [Fact]
    public void TheLayoutIsReadForTheDefaultKey_OrForTheKeyOfTheParameter()
    {
        RegisterThreeParts();
        RenderWithStore();
        Render<RoleCenter>(parameters => parameters.Add(p => p.LayoutKey, "/reports"));

        Assert.Equal(["/", "/reports"], Store.GetKeys);
    }

    [Fact]
    public void SavedLayout_HidesPartsBeforeTheyAreRenderedOrFetched()
    {
        Probe.LoadGate = new TaskCompletionSource();
        Services.AddBlazorBaseDashboard(options => options
            .AddPart<PartA>("alpha", priority: 1)
            .AddPart<LoadingPart>("loading", priority: 2, title: "Loading part"));
        Store.Stored = Layout(("alpha", false), ("loading", true));

        var cut = RenderWithStore();

        Assert.Equal(["PartA"], PartNames(cut));
        Assert.Empty(cut.FindAll(".loading-part"));
        Assert.Equal(0, Probe.Loads);
        Assert.Empty(cut.FindAll("[data-part-id='loading']"));
    }

    [Fact]
    public void SavedLayout_OverridesThePriorityOrder()
    {
        RegisterThreeParts();
        Store.Stored = Layout(("gamma", false), ("alpha", false), ("beta", false));

        var cut = RenderWithStore();

        Assert.Equal(["PartC", "PartA", "PartB"], PartNames(cut));
    }

    [Fact]
    public void UnknownIdsInTheLayout_AreIgnored_AndNewPartsKeepTheirDefaultPosition()
    {
        RegisterThreeParts();
        Store.Stored = Layout(("removed-part", true), ("gamma", false), ("alpha", false));

        var cut = RenderWithStore();

        Assert.Equal(["PartC", "PartA", "PartB"], PartNames(cut));
    }

    [Fact]
    public void RoleRulesWinOverTheLayout_ALayoutNeverRevealsAPart()
    {
        Services.AddBlazorBaseDashboard(options => options
            .AddPart<PartA>("alpha", priority: 1)
            .AddPart<PartB>("admins", priority: 2, isVisibleAsync: DashboardVisibility.ForRoles("Admin"))
            .AddPart<PartC>("gamma", priority: 3));
        Store.Stored = Layout(("admins", false), ("gamma", false), ("alpha", false));

        var cut = RenderWithStore();

        Assert.Equal(["PartC", "PartA"], PartNames(cut));

        StartEditing(cut);
        Assert.Equal(["Gamma", "Alpha"], CardTitles(cut));
    }

    [Fact]
    public void AnAdmin_SeesThePartTheLayoutPlacesFirst()
    {
        Services.AddBlazorBaseDashboard(options => options
            .AddPart<PartA>("alpha", priority: 1)
            .AddPart<PartB>("admins", priority: 2, isVisibleAsync: DashboardVisibility.ForRoles("Admin")));
        Authorization.SetRoles("Admin");
        Store.Stored = Layout(("admins", false), ("alpha", false));

        var cut = RenderWithStore();

        Assert.Equal(["PartB", "PartA"], PartNames(cut));
    }

    [Fact]
    public void StoreThatFailsOnLoad_FallsBackToTheDefaultLayout_WithoutBreakingThePage()
    {
        RegisterThreeParts();
        Store.GetFailure = new HttpRequestException("The server is unreachable.");

        var cut = RenderWithStore();

        Assert.Equal(["PartA", "PartB", "PartC"], PartNames(cut));
        Assert.NotEmpty(cut.FindAll(".rc-personalize"));
        Assert.DoesNotContain("unreachable", cut.Markup);
    }

    [Fact]
    public void StoreThatNeverAnswers_FallsBackToTheDefaultLayoutAfterTheTimeout()
    {
        RegisterThreeParts();
        Store.GetGate = new TaskCompletionSource();
        Store.Stored = Layout(("gamma", false), ("alpha", true), ("beta", false));

        var cut = RenderWithStore();
        Assert.Empty(PartNames(cut));

        Time.Advance(TimeSpan.FromSeconds(6));

        cut.WaitForAssertion(() => Assert.Equal(["PartA", "PartB", "PartC"], PartNames(cut)));
    }

    [Fact]
    public void Personalize_ListsEveryPartTheUserMaySee_WithTheControlSet()
    {
        RegisterThreeParts();
        var cut = RenderWithStore();

        StartEditing(cut);

        Assert.Equal(["Alpha", "Beta", "Gamma"], CardTitles(cut));
        Assert.Empty(cut.FindAll(".rc-personalize"));
        Assert.Equal(3, cut.FindAll("[data-edit-action='up']").Count);
        Assert.Equal(3, cut.FindAll("[data-edit-action='down']").Count);
        Assert.Equal(3, cut.FindAll("[data-edit-action='visibility']").Count);
        Assert.All(cut.FindAll(".rc-edit-button"), button => Assert.Equal("button", button.GetAttribute("type")));
        Assert.Contains("Personalize your dashboard", cut.Find(".rc-edit-bar").TextContent);
        Assert.Equal(["PartA", "PartB", "PartC"], PartNames(cut));
    }

    [Fact]
    public void EditControls_HaveAccessibleNamesThatIncludeThePartTitle_AndMarkTheEndsAsDisabled()
    {
        RegisterThreeParts();
        var cut = RenderWithStore();

        StartEditing(cut);

        Assert.Equal("Move Alpha up", cut.Find("[data-edit-part='alpha'][data-edit-action='up']").GetAttribute("aria-label"));
        Assert.Equal("Move Beta down", cut.Find("[data-edit-part='beta'][data-edit-action='down']").GetAttribute("aria-label"));
        Assert.Equal("Hide Gamma", cut.Find("[data-edit-part='gamma'][data-edit-action='visibility']").GetAttribute("aria-label"));
        Assert.Equal("true", cut.Find("[data-edit-part='alpha'][data-edit-action='up']").GetAttribute("aria-disabled"));
        Assert.Null(cut.Find("[data-edit-part='beta'][data-edit-action='up']").GetAttribute("aria-disabled"));
        Assert.Equal("true", cut.Find("[data-edit-part='gamma'][data-edit-action='down']").GetAttribute("aria-disabled"));
        Assert.All(cut.FindAll(".rc-slot-body"), body => Assert.True(body.HasAttribute("inert")));
    }

    [Fact]
    public void PartWithoutATitle_IsListedUnderAHumanizedId()
    {
        Services.AddBlazorBaseDashboard(options => options.AddPart<PartA>("my-ai-usage"));
        var cut = RenderWithStore();

        StartEditing(cut);

        Assert.Equal(["My ai usage"], CardTitles(cut));
    }

    [Fact]
    public void MoveUpAndDown_ReorderTheCards_AndTheParts()
    {
        RegisterThreeParts();
        var cut = RenderWithStore();
        StartEditing(cut);

        Press(cut, "gamma", "up");
        Assert.Equal(["Alpha", "Gamma", "Beta"], CardTitles(cut));
        Assert.Equal(["PartA", "PartC", "PartB"], PartNames(cut));

        Press(cut, "alpha", "down");
        Assert.Equal(["Gamma", "Alpha", "Beta"], CardTitles(cut));
        Assert.Equal(["PartC", "PartA", "PartB"], PartNames(cut));
    }

    [Fact]
    public void MovingPastTheEnds_ChangesNothing_AndSaysSo()
    {
        RegisterThreeParts();
        var cut = RenderWithStore();
        StartEditing(cut);

        Press(cut, "alpha", "up");
        Assert.Equal(["Alpha", "Beta", "Gamma"], CardTitles(cut));
        Assert.Equal("Alpha is already the first part.", Announcement(cut));

        Press(cut, "gamma", "down");
        Assert.Equal("Gamma is already the last part.", Announcement(cut));
    }

    [Fact]
    public void Announcements_GoToAPoliteLiveRegion()
    {
        RegisterThreeParts();
        var cut = RenderWithStore();
        var region = cut.Find(".rc-sr-only");
        Assert.Equal("status", region.GetAttribute("role"));
        Assert.Equal("polite", region.GetAttribute("aria-live"));
        Assert.Equal("true", region.GetAttribute("aria-atomic"));

        StartEditing(cut);
        Assert.Contains("Personalize mode is on", Announcement(cut));

        Press(cut, "beta", "up");
        Assert.Equal("Beta moved to position 1 of 3.", Announcement(cut));

        Press(cut, "gamma", "visibility");
        Assert.Equal("Gamma hidden.", Announcement(cut));

        Press(cut, "gamma", "visibility");
        Assert.Equal("Gamma shown.", Announcement(cut));
    }

    [Fact]
    public void RepeatedIdenticalAnnouncements_StillChangeTheLiveRegionText()
    {
        RegisterThreeParts();
        var cut = RenderWithStore();
        StartEditing(cut);

        Press(cut, "alpha", "up");
        var first = cut.Find(".rc-sr-only").TextContent;
        Press(cut, "alpha", "up");
        var second = cut.Find(".rc-sr-only").TextContent;

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void HidingAPart_KeepsItListedAsADimmedCardWithShow_AndStopsRenderingIt()
    {
        RegisterThreeParts();
        var cut = RenderWithStore();
        StartEditing(cut);

        Press(cut, "beta", "visibility");

        Assert.Equal(["Alpha", "Beta", "Gamma"], CardTitles(cut));
        Assert.Equal(["PartA", "PartC"], PartNames(cut));
        Assert.Contains("rc-slot--hidden", cut.Find("[data-slot-id='beta']").ClassName);
        Assert.Contains("Hidden", cut.Find("[data-slot-id='beta'] .rc-edit-card-badge").TextContent);
        var show = cut.Find("[data-edit-part='beta'][data-edit-action='visibility']");
        Assert.Equal("Show Beta", show.GetAttribute("aria-label"));
        Assert.Contains("Show", show.TextContent);

        show.Click();

        Assert.Equal(["PartA", "PartB", "PartC"], PartNames(cut));
        Assert.DoesNotContain("rc-slot--hidden", cut.Find("[data-slot-id='beta']").ClassName);
    }

    [Fact]
    public void ShowingAHiddenPart_FetchesItOnlyThen()
    {
        Services.AddBlazorBaseDashboard(options => options
            .AddPart<PartA>("alpha", priority: 1, title: "Alpha")
            .AddPart<LoadingPart>("loading", priority: 2, title: "Usage"));
        Store.Stored = Layout(("alpha", false), ("loading", true));
        var cut = RenderWithStore();
        Assert.Equal(0, Probe.Loads);

        StartEditing(cut);
        Press(cut, "loading", "visibility");

        cut.WaitForAssertion(() => Assert.Equal(1, Probe.Loads));
    }

    [Fact]
    public void Done_SavesTheArrangementUnderTheKey_AndLeavesTheEditMode()
    {
        RegisterThreeParts();
        var cut = RenderWithStore(parameters => parameters.Add(p => p.LayoutKey, "/home"));
        StartEditing(cut);
        Press(cut, "gamma", "up");
        Press(cut, "alpha", "visibility");

        cut.Find(".rc-edit-done").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".rc-edit-bar")));
        var (key, saved) = Assert.Single(Store.Saves);
        Assert.Equal("/home", key);
        Assert.Equal(["alpha", "gamma", "beta"], saved.Parts.Select(part => part.PartId));
        Assert.Equal([true, false, false], saved.Parts.Select(part => part.Hidden));
        Assert.Equal([0, 1, 2], saved.Parts.Select(part => part.Order));
        Assert.Equal(["PartC", "PartB"], PartNames(cut));
        Assert.NotEmpty(cut.FindAll(".rc-personalize"));
        Assert.Equal("Layout saved.", Announcement(cut));
        Assert.Empty(cut.FindAll(".rc-edit-card"));
    }

    [Fact]
    public void ASavedLayout_IsAppliedByTheNextRoleCenter()
    {
        RegisterThreeParts();
        var first = RenderWithStore();
        StartEditing(first);
        Press(first, "gamma", "up");
        Press(first, "gamma", "up");
        first.Find(".rc-edit-done").Click();
        first.WaitForAssertion(() => Assert.Single(Store.Saves));

        var second = Render<RoleCenter>();

        Assert.Equal(["PartC", "PartA", "PartB"], PartNames(second));
    }

    [Fact]
    public void Cancel_DiscardsTheChanges_AndSavesNothing()
    {
        RegisterThreeParts();
        var cut = RenderWithStore();
        StartEditing(cut);
        Press(cut, "gamma", "up");
        Press(cut, "alpha", "visibility");

        cut.Find(".rc-edit-cancel").Click();

        Assert.Empty(Store.Saves);
        Assert.Empty(cut.FindAll(".rc-edit-bar"));
        Assert.Equal(["PartA", "PartB", "PartC"], PartNames(cut));
        Assert.Equal("Changes discarded.", Announcement(cut));
    }

    [Fact]
    public void SaveFailure_KeepsTheEditMode_ShowsAnAlert_AndKeepsTheChanges()
    {
        RegisterThreeParts();
        Store.SaveFailure = new HttpRequestException("The server is unreachable.");
        var cut = RenderWithStore();
        StartEditing(cut);
        Press(cut, "gamma", "up");

        cut.Find(".rc-edit-done").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".rc-edit-error")));
        var alert = cut.Find(".rc-edit-error");
        Assert.Equal("alert", alert.GetAttribute("role"));
        Assert.Contains("could not be saved", alert.TextContent);
        Assert.DoesNotContain("unreachable", cut.Markup);
        Assert.Equal(["Alpha", "Gamma", "Beta"], CardTitles(cut));
        Assert.Empty(Store.Saves);

        Store.SaveFailure = null;
        cut.Find(".rc-edit-done").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".rc-edit-bar")));
        Assert.Single(Store.Saves);
    }

    [Fact]
    public void Reset_AsksForConfirmation_BeforeAnythingIsDeleted()
    {
        RegisterThreeParts();
        Store.Stored = Layout(("gamma", false), ("alpha", true), ("beta", false));
        var cut = RenderWithStore();
        StartEditing(cut);

        cut.Find(".rc-edit-reset").Click();

        var confirm = cut.Find(".rc-edit-confirm");
        Assert.Contains("Reset the dashboard to the default layout?", confirm.TextContent);
        Assert.Empty(Store.Resets);

        cut.Find(".rc-edit-confirm-no").Click();

        Assert.Empty(cut.FindAll(".rc-edit-confirm"));
        Assert.Empty(Store.Resets);
        Assert.Equal(["Gamma", "Alpha", "Beta"], CardTitles(cut));
    }

    [Fact]
    public void ConfirmedReset_DeletesTheLayout_AndShowsTheDefaultArrangement()
    {
        RegisterThreeParts();
        Store.Stored = Layout(("gamma", false), ("alpha", true), ("beta", false));
        var cut = RenderWithStore(parameters => parameters.Add(p => p.LayoutKey, "/home"));
        Assert.Equal(["PartC", "PartB"], PartNames(cut));
        StartEditing(cut);

        cut.Find(".rc-edit-reset").Click();
        cut.Find(".rc-edit-confirm-yes").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".rc-edit-bar")));
        Assert.Equal(["/home"], Store.Resets);
        Assert.Equal(["PartA", "PartB", "PartC"], PartNames(cut));
        Assert.Equal("Layout reset to default.", Announcement(cut));
    }

    [Fact]
    public void ResetFailure_ShowsAnAlert_AndKeepsTheLayout()
    {
        RegisterThreeParts();
        Store.Stored = Layout(("gamma", false), ("alpha", false), ("beta", false));
        Store.ResetFailure = new HttpRequestException("The server is unreachable.");
        var cut = RenderWithStore();
        StartEditing(cut);

        cut.Find(".rc-edit-reset").Click();
        cut.Find(".rc-edit-confirm-yes").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".rc-edit-error")));
        Assert.Contains("could not be reset", cut.Find(".rc-edit-error").TextContent);
        Assert.NotEmpty(cut.FindAll(".rc-edit-bar"));
        Assert.Empty(cut.FindAll(".rc-edit-confirm"));
        Assert.Equal(["Gamma", "Alpha", "Beta"], CardTitles(cut));
    }

    [Fact]
    public void EditMode_MovesTheFocusToTheControlThatWasUsed_AndBackToPersonalizeAfterwards()
    {
        var module = DashboardModule;
        module.Setup<bool>("focusControl", _ => true).SetResult(true);
        module.Setup<bool>("focusById", _ => true).SetResult(true);
        RegisterThreeParts();
        var cut = RenderWithStore();

        StartEditing(cut);
        cut.WaitForAssertion(() => Assert.Single(module.Invocations["focusById"]));
        Assert.EndsWith("-edit-title", (string)module.Invocations["focusById"].Single().Arguments[0]!);

        Press(cut, "gamma", "up");
        cut.WaitForAssertion(() => Assert.Single(module.Invocations["focusControl"]));
        Assert.Equal(["gamma", "up"], module.Invocations["focusControl"].Single().Arguments.Select(argument => (string)argument!));

        cut.Find(".rc-edit-cancel").Click();
        cut.WaitForAssertion(() => Assert.Equal(2, module.Invocations["focusById"].Count));
        Assert.EndsWith("-personalize", (string)module.Invocations["focusById"][1].Arguments[0]!);
    }

    [Fact]
    public void GermanCulture_TranslatesTheEditMode()
    {
        using var culture = new CultureScope("de-DE");
        RegisterThreeParts();
        var cut = RenderWithStore();

        Assert.Contains("Anpassen", cut.Find(".rc-personalize").TextContent);
        StartEditing(cut);

        Assert.Contains("Dashboard anpassen", cut.Find(".rc-edit-bar").TextContent);
        Assert.Equal("Alpha nach oben verschieben", cut.Find("[data-edit-part='alpha'][data-edit-action='up']").GetAttribute("aria-label"));
        Assert.Equal("Beta ausblenden", cut.Find("[data-edit-part='beta'][data-edit-action='visibility']").GetAttribute("aria-label"));
        Assert.Contains("Fertig", cut.Find(".rc-edit-done").TextContent);
        Assert.Contains("Abbrechen", cut.Find(".rc-edit-cancel").TextContent);
        Assert.Contains("Auf Standard zurücksetzen", cut.Find(".rc-edit-reset").TextContent);
    }

    [Fact]
    public void RoleChange_DuringEditing_DropsPartsTheUserNoLongerMaySee()
    {
        Services.AddBlazorBaseDashboard(options => options
            .AddPart<PartA>("alpha", priority: 1, title: "Alpha")
            .AddPart<PartB>("admins", priority: 2, title: "Admins", isVisibleAsync: DashboardVisibility.ForRoles("Admin")));
        Authorization.SetRoles("Admin");
        var cut = RenderWithStore();
        StartEditing(cut);
        Assert.Equal(["Alpha", "Admins"], CardTitles(cut));

        Authorization.SetRoles("Other");
        cut.InvokeAsync(() => cut.Instance.RefreshService.RefreshAllAsync());

        cut.WaitForAssertion(() => Assert.Equal(["Alpha"], CardTitles(cut)));
    }
}
