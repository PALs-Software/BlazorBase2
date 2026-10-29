using BlazorBase.Dashboard.Test.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace BlazorBase.Dashboard.Test.Layout;

public sealed class RoleCenterTests : DashboardTestContext
{
    private readonly PartProbe Probe = new();

    public RoleCenterTests() => Services.AddSingleton(Probe);

    private IRenderedComponent<RoleCenter> RenderRoleCenter(Action<ComponentParameterCollectionBuilder<RoleCenter>>? configure = null) =>
        Render<RoleCenter>(parameters => configure?.Invoke(parameters));

    private static IEnumerable<string> PartNames(IRenderedComponent<RoleCenter> cut) =>
        cut.FindAll(".named-part").Select(element => element.TextContent);

    [Fact]
    public void Parts_AreRenderedInPriorityOrder_NotInRegistrationOrder()
    {
        Services.AddBlazorBaseDashboard(options => options
            .AddPart<PartC>("c", priority: 30)
            .AddPart<PartA>("a", priority: 10)
            .AddPart<PartB>("b", priority: 20));

        var cut = RenderRoleCenter();

        Assert.Equal(["PartA", "PartB", "PartC"], PartNames(cut));
    }

    [Fact]
    public void PartsWithTheSamePriority_KeepTheirRegistrationOrder()
    {
        Services.AddBlazorBaseDashboard(options => options
            .AddPart<PartB>("b", priority: 5)
            .AddPart<PartA>("a", priority: 5));

        var cut = RenderRoleCenter();

        Assert.Equal(["PartB", "PartA"], PartNames(cut));
    }

    [Fact]
    public void ColumnWidth_IsHandedToTheGridCell_AndClampedToTwelve()
    {
        Services.AddBlazorBaseDashboard(options => options
            .AddPart<PartA>("a", columns: 7)
            .AddPart<PartB>("b", columns: 40));

        var cut = RenderRoleCenter();

        var cells = cut.FindAll(".pf");
        Assert.Contains("--pf-columns: 7", cells[0].GetAttribute("style"));
        Assert.Contains("--pf-columns: 12", cells[1].GetAttribute("style"));
    }

    [Fact]
    public void RoleRestrictedPart_IsHiddenFromOtherUsers_AndShownToItsRole()
    {
        Services.AddBlazorBaseDashboard(options => options
            .AddPart<PartA>("everyone", priority: 1)
            .AddPart<PartB>("admins", priority: 2, isVisibleAsync: DashboardVisibility.ForRoles("Admin"))
            .AddPart<PartC>("approvers", priority: 3, isVisibleAsync: DashboardVisibility.ForRoles("Admin", "Approver")));
        Authorization.SetRoles("Approver");

        var cut = RenderRoleCenter();

        Assert.Equal(["PartA", "PartC"], PartNames(cut));
    }

    [Fact]
    public void AdminRole_SeesAllRoleRestrictedParts()
    {
        Services.AddBlazorBaseDashboard(options => options
            .AddPart<PartA>("everyone", priority: 1)
            .AddPart<PartB>("admins", priority: 2, isVisibleAsync: DashboardVisibility.ForRoles("Admin")));
        Authorization.SetRoles("Admin");

        var cut = RenderRoleCenter();

        Assert.Equal(["PartA", "PartB"], PartNames(cut));
    }

    [Fact]
    public void UserWithoutRoles_SeesOnlyThePartsForAnyUser()
    {
        Services.AddBlazorBaseDashboard(options => options
            .AddPart<PartA>("everyone", priority: 1, isVisibleAsync: DashboardVisibility.ForAnyUser())
            .AddPart<PartB>("admins", priority: 2, isVisibleAsync: DashboardVisibility.ForRoles("Admin")));

        var cut = RenderRoleCenter();

        Assert.Equal(["PartA"], PartNames(cut));
    }

    [Fact]
    public void VisibilityRuleThatThrows_HidesThePart_WithoutBreakingThePage()
    {
        Services.AddBlazorBaseDashboard(options => options
            .AddPart<PartA>("fine", priority: 1)
            .AddPart<PartB>("broken", priority: 2, isVisibleAsync: _ => throw new InvalidOperationException("policy store down")));

        var cut = RenderRoleCenter();

        Assert.Equal(["PartA"], PartNames(cut));
    }

    [Fact]
    public void FailingPart_ShowsARetryCard_AndTheOtherPartsStayOnThePage()
    {
        Probe.ShouldFail = true;
        Services.AddBlazorBaseDashboard(options => options
            .AddPart<PartA>("a", priority: 1)
            .AddPart<FailingPart>("failing", priority: 2, title: "Failing")
            .AddPart<PartB>("b", priority: 3));

        var cut = RenderRoleCenter();

        Assert.Equal(["PartA", "PartB"], PartNames(cut));
        var card = cut.Find(".pf-error");
        Assert.Equal("alert", card.GetAttribute("role"));
        Assert.Contains("This part could not be loaded.", card.TextContent);
        Assert.Contains("Failing", card.TextContent);
        Assert.DoesNotContain("The part is broken.", cut.Markup);
    }

    [Fact]
    public void RetryOnTheErrorCard_RecreatesThePart()
    {
        Probe.ShouldFail = true;
        Services.AddBlazorBaseDashboard(options => options.AddPart<FailingPart>("failing"));
        var cut = RenderRoleCenter();
        Assert.NotEmpty(cut.FindAll(".pf-error"));

        Probe.ShouldFail = false;
        cut.Find(".pf-retry").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".recovered-part")));
        Assert.Empty(cut.FindAll(".pf-error"));
    }

    [Fact]
    public void RefreshButton_RefreshesEveryRefreshablePart_AndUpdatesTheTimestamp()
    {
        Services.AddBlazorBaseDashboard(options => options
            .AddPart<RefreshablePart>("refreshable")
            .AddPart<PartA>("plain"));
        var cut = RenderRoleCenter();
        Assert.Contains("Updated 09:24", cut.Find(".rc-updated").TextContent);

        Time.Advance(TimeSpan.FromMinutes(2));
        cut.Find(".rc-refresh").Click();

        cut.WaitForAssertion(() => Assert.Equal(1, Probe.Refreshes));
        cut.WaitForAssertion(() => Assert.Contains("Updated 09:26", cut.Find(".rc-updated").TextContent));
    }

    [Fact]
    public void RefreshingAPart_ThatThrows_ShowsItsRetryCard_AndKeepsTheOthers()
    {
        Services.AddBlazorBaseDashboard(options => options
            .AddPart<RefreshablePart>("refreshable", priority: 1)
            .AddPart<PartA>("plain", priority: 2));
        var cut = RenderRoleCenter();
        Probe.ShouldFailOnRefresh = true;

        cut.Find(".rc-refresh").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".pf-error")));
        Assert.Equal(["PartA"], PartNames(cut));
    }

    [Fact]
    public void APart_CanRefreshAnotherPartByIdThroughTheCascadedService()
    {
        Services.AddBlazorBaseDashboard(options => options
            .AddPart<RefreshablePart>("refreshable", priority: 1)
            .AddPart<RefreshRequestingPart>("requester", priority: 2));
        var cut = RenderRoleCenter();

        cut.Find(".request-refresh").Click();

        cut.WaitForAssertion(() => Assert.Equal(1, Probe.Refreshes));
    }

    [Fact]
    public async Task RefreshAll_ThroughTheExposedService_RefreshesTheHeadlineSlotToo()
    {
        Services.AddBlazorBaseDashboard(options => options.AddPart<RefreshablePart>("refreshable"));
        var provider = new CountingInsightProvider();
        Services.AddSingleton<IHeadlineInsightProvider>(provider);
        var cut = RenderRoleCenter(parameters => parameters.Add(p => p.Headline, (RenderFragment)(builder =>
        {
            builder.OpenComponent<HeadlinePart>(0);
            builder.CloseComponent();
        })));
        var before = provider.Calls;

        await cut.InvokeAsync(() => cut.Instance.RefreshService.RefreshAllAsync());

        Assert.Equal(1, Probe.Refreshes);
        Assert.Equal(before + 1, provider.Calls);
    }

    [Fact]
    public void PartThatReportsEmpty_IsCollapsed_WithoutAffectingTheOthers()
    {
        Probe.ReportsEmpty = true;
        Services.AddBlazorBaseDashboard(options => options
            .AddPart<EmptyingPart>("emptying", priority: 1)
            .AddPart<PartA>("other", priority: 2));

        var cut = RenderRoleCenter();

        cut.WaitForAssertion(() => Assert.Contains("pf--collapsed", cut.Find("[data-part-id='emptying']").ClassName));
        Assert.DoesNotContain("pf--collapsed", cut.Find("[data-part-id='other']").ClassName ?? String.Empty);
    }

    [Fact]
    public void PartThatLoads_ShowsASkeleton_UntilItIsLoaded()
    {
        Probe.LoadGate = new TaskCompletionSource();
        Services.AddBlazorBaseDashboard(options => options.AddPart<LoadingPart>("loading", title: "Usage"));

        var cut = RenderRoleCenter();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".pf-skeleton")));
        Assert.Equal("true", cut.Find("[data-part-id='loading']").GetAttribute("aria-busy"));

        Probe.LoadGate.SetResult();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".pf-skeleton")));
        Assert.Null(cut.Find("[data-part-id='loading']").GetAttribute("aria-busy"));
    }

    [Fact]
    public void AutoRefresh_RunsAfterTheInterval_ForParts()
    {
        Services.AddBlazorBaseDashboard(options => options.AddPart<RefreshablePart>("refreshable"));
        var cut = RenderRoleCenter();
        cut.WaitForState(() => Time.ActiveTimers == 1);

        Time.Advance(TimeSpan.FromMinutes(5));

        cut.WaitForAssertion(() => Assert.Equal(1, Probe.Refreshes));
    }

    [Fact]
    public void AutoRefreshInterval_ConfiguredOnTheOptions_IsUsed()
    {
        Services.AddBlazorBaseDashboard(options =>
        {
            options.AutoRefreshInterval = TimeSpan.FromMinutes(1);
            options.AddPart<RefreshablePart>("refreshable");
        });
        var cut = RenderRoleCenter();
        cut.WaitForState(() => Time.ActiveTimers == 1);

        Time.Advance(TimeSpan.FromMinutes(1));

        cut.WaitForAssertion(() => Assert.Equal(1, Probe.Refreshes));
    }

    [Fact]
    public void AutoRefreshInterval_ParameterOverridesTheOptions()
    {
        Services.AddBlazorBaseDashboard(options => options.AddPart<RefreshablePart>("refreshable"));
        var cut = RenderRoleCenter(parameters => parameters.Add(p => p.AutoRefreshInterval, TimeSpan.FromSeconds(30)));
        cut.WaitForState(() => Time.ActiveTimers == 1);

        Time.Advance(TimeSpan.FromSeconds(30));

        cut.WaitForAssertion(() => Assert.Equal(1, Probe.Refreshes));
    }

    [Fact]
    public void AutoRefreshOff_StartsNoTimer()
    {
        Services.AddBlazorBaseDashboard(options => options.AddPart<RefreshablePart>("refreshable"));

        var cut = RenderRoleCenter(parameters => parameters.Add(p => p.AutoRefreshInterval, TimeSpan.Zero));

        Assert.Equal(0, Time.ActiveTimers);
        Assert.NotEmpty(cut.FindAll(".refreshable-part"));
    }

    [Fact]
    public async Task AutoRefresh_IsSkippedWhileTheTabIsHidden_AndCaughtUpWhenItIsShownAgain()
    {
        var module = DashboardModule;
        module.Setup<bool>("isTabVisible").SetResult(true);
        module.Setup<int>("observeVisibility", _ => true).SetResult(1);
        Services.AddBlazorBaseDashboard(options => options.AddPart<RefreshablePart>("refreshable"));
        var cut = RenderRoleCenter();
        cut.WaitForState(() => Time.ActiveTimers == 1);
        var callback = ((DotNetObjectReference<DashboardJsCallback>)module.Invocations["observeVisibility"].Single().Arguments[0]!).Value;

        await cut.InvokeAsync(() => callback.Invoke(false));
        Time.Advance(TimeSpan.FromMinutes(5));
        await Task.Delay(50);
        Assert.Equal(0, Probe.Refreshes);

        await cut.InvokeAsync(() => callback.Invoke(true));

        cut.WaitForAssertion(() => Assert.Equal(1, Probe.Refreshes));
    }

    [Fact]
    public async Task ShowingTheTabAgain_RefreshesWhenTheDataIsOlderThanTheInterval()
    {
        var module = DashboardModule;
        module.Setup<bool>("isTabVisible").SetResult(true);
        module.Setup<int>("observeVisibility", _ => true).SetResult(1);
        Services.AddBlazorBaseDashboard(options => options.AddPart<RefreshablePart>("refreshable"));
        var cut = RenderRoleCenter();
        var callback = ((DotNetObjectReference<DashboardJsCallback>)module.Invocations["observeVisibility"].Single().Arguments[0]!).Value;
        Time.SetUtcNow(Time.GetUtcNow() + TimeSpan.FromMinutes(20));

        await cut.InvokeAsync(() => callback.Invoke(true));

        cut.WaitForAssertion(() => Assert.Equal(1, Probe.Refreshes));
    }

    [Fact]
    public async Task Dispose_StopsTheTimer()
    {
        Services.AddBlazorBaseDashboard(options => options.AddPart<RefreshablePart>("refreshable"));
        var cut = RenderRoleCenter();
        cut.WaitForState(() => Time.ActiveTimers == 1);

        await DisposeComponentsAsync();
        Time.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(0, Probe.Refreshes);
    }

    [Fact]
    public void RefreshButton_IsGerman_InTheGermanCulture()
    {
        using var culture = new CultureScope("de-DE");
        Services.AddBlazorBaseDashboard(options => options.AddPart<PartA>("a"));

        var cut = RenderRoleCenter();

        Assert.Contains("Aktualisieren", cut.Find(".rc-refresh").TextContent);
        Assert.Contains("Aktualisiert 09:24", cut.Find(".rc-updated").TextContent);
    }

    [Fact]
    public void RoleCenter_IsALabelledRegion_WithTheGridInside()
    {
        Services.AddBlazorBaseDashboard(options => options.AddPart<PartA>("a"));

        var cut = RenderRoleCenter();

        var root = cut.Find(".rc");
        Assert.Equal("region", root.GetAttribute("role"));
        Assert.Equal("Dashboard", root.GetAttribute("aria-label"));
        Assert.NotNull(cut.Find(".rc-grid .pf"));
    }

    [Fact]
    public void HeadlineMetaAndToolsSlots_AreRendered()
    {
        Services.AddBlazorBaseDashboard(options => options.AddPart<PartA>("a"));

        var cut = RenderRoleCenter(parameters => parameters
            .Add(p => p.Headline, (RenderFragment)(builder => builder.AddMarkupContent(0, "<div class=\"slot-headline\">H</div>")))
            .Add(p => p.MetaContent, (RenderFragment)(builder => builder.AddMarkupContent(0, "<span class=\"slot-meta\">October 2026</span>")))
            .Add(p => p.Tools, (RenderFragment)(builder => builder.AddMarkupContent(0, "<span class=\"slot-tools\">Personalize</span>"))));

        Assert.NotNull(cut.Find(".slot-headline"));
        Assert.Equal("October 2026", cut.Find(".rc-meta-start .slot-meta").TextContent);
        Assert.Equal("Personalize", cut.Find(".rc-meta-end .slot-tools").TextContent);
    }
}
