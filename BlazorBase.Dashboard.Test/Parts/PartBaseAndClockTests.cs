using BlazorBase.Dashboard.Test.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.Dashboard.Test.Parts;

public sealed class PartBaseAndClockTests : DashboardTestContext
{
    private readonly PartProbe Probe = new();

    public PartBaseAndClockTests() => Services.AddSingleton(Probe);

    private IRenderedComponent<CascadingValue<IDashboardPartHost>> RenderInHost<TPart>(RecordingHost host)
        where TPart : Microsoft.AspNetCore.Components.IComponent =>
        Render<CascadingValue<IDashboardPartHost>>(p => p.Add(c => c.Value, host).AddChildContent<TPart>());

    [Fact]
    public void PartBase_ReportsLoadingAndLoaded_AroundTheFirstLoad()
    {
        var host = new RecordingHost();
        Probe.LoadGate = new TaskCompletionSource();

        var cut = RenderInHost<LoadingPart>(host);

        Assert.Equal(1, host.LoadingReports);
        Assert.Equal(0, host.LoadedReports);

        Probe.LoadGate.SetResult();

        cut.WaitForAssertion(() => Assert.Equal(1, host.LoadedReports));
        Assert.Equal(1, host.LoadingReports);
    }

    [Fact]
    public void PartBase_ReportsLoaded_EvenWhenTheLoadFails()
    {
        var host = new RecordingHost();

        Assert.ThrowsAny<Exception>(() => RenderInHost<FailingLoadPart>(host));

        Assert.Equal(1, host.LoadingReports);
        Assert.Equal(1, host.LoadedReports);
    }

    [Fact]
    public async Task Refresh_RunsTheLoadAgain_WithoutReportingLoadingAgain()
    {
        var host = new RecordingHost();
        var cut = RenderInHost<LoadingPart>(host);
        var part = cut.FindComponent<LoadingPart>();

        await cut.InvokeAsync(() => part.Instance.RefreshAsync());

        Assert.Equal(2, Probe.Loads);
        Assert.Equal(1, host.LoadingReports);
    }

    [Fact]
    public void PartBase_WorksOutsideARoleCenter_WithoutAHost()
    {
        var cut = Render<LoadingPart>();

        Assert.Contains("Loaded 1", cut.Markup);
    }

    [Fact]
    public async Task DisposalToken_IsCancelledWhenThePartIsRemoved()
    {
        var cut = Render<LoadingPart>();
        var part = cut.Instance;
        Assert.False(part.IsCancelled);

        await DisposeComponentsAsync();

        Assert.True(part.IsCancelled);
    }

    [Fact]
    public async Task Clock_UsesTheBrowserOffset_OnceItIsKnown()
    {
        DashboardModule.Setup<int?>("getUtcOffsetMinutes").SetResult(120);
        var clock = Services.GetRequiredService<DashboardClock>();
        Assert.Equal(TimeSpan.Zero, clock.LocalNow.Offset);

        var changed = await clock.EnsureInitializedAsync();

        Assert.True(changed);
        Assert.Equal(TimeSpan.FromHours(2), clock.LocalNow.Offset);
        Assert.Equal(11, clock.LocalNow.Hour);
        Assert.False(await clock.EnsureInitializedAsync());
    }

    [Fact]
    public async Task Clock_KeepsTheProviderTimeZone_WhenTheBrowserCannotAnswer()
    {
        var clock = Services.GetRequiredService<DashboardClock>();

        var changed = await clock.EnsureInitializedAsync();

        Assert.False(changed);
        Assert.Equal(9, clock.LocalNow.Hour);
    }

    [Fact]
    public void Greeting_FollowsTheBrowserTimeZone_NotTheServers()
    {
        DashboardModule.Setup<int?>("getUtcOffsetMinutes").SetResult(600);

        var cut = Render<HeadlinePart>();

        cut.WaitForAssertion(() => Assert.Equal("Good evening", cut.Find(".hl-greeting").TextContent));
    }

    [Fact]
    public void RoleCenter_ShowsTheUpdatedTime_InTheBrowserTimeZone()
    {
        DashboardModule.Setup<int?>("getUtcOffsetMinutes").SetResult(120);
        Services.AddBlazorBaseDashboard(options => options.AddPart<PartA>("a"));

        var cut = Render<RoleCenter>();

        cut.WaitForAssertion(() => Assert.Contains("11:24", cut.Find(".rc-updated").TextContent));
    }
}
