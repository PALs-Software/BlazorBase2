using BlazorBase.Dashboard.Test.Infrastructure;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.Dashboard.Test.Parts;

public sealed class RegistrationTests
{
    [Fact]
    public void Registry_OrdersByPriority_AndKeepsRegistrationOrderForTies()
    {
        var registry = new DashboardPartRegistry();
        registry.Register(DashboardPartRegistration.For<PartA>("a", priority: 20));
        registry.Register(DashboardPartRegistration.For<PartB>("b", priority: 10));
        registry.Register(DashboardPartRegistration.For<PartC>("c", priority: 20));

        Assert.Equal(["b", "a", "c"], registry.Parts.Select(part => part.Id));
    }

    [Fact]
    public void Registry_RejectsADuplicateId() =>
        Assert.Throws<InvalidOperationException>(() =>
        {
            var registry = new DashboardPartRegistry();
            registry.Register(DashboardPartRegistration.For<PartA>("a"));
            registry.Register(DashboardPartRegistration.For<PartB>("a"));
        });

    [Fact]
    public void Registry_RejectsATypeThatIsNoComponent() =>
        Assert.Throws<InvalidOperationException>(() => new DashboardPartRegistry().Register(new DashboardPartRegistration { Id = "x", ComponentType = typeof(string) }));

    [Fact]
    public void Registry_RejectsAMissingId() =>
        Assert.Throws<InvalidOperationException>(() => new DashboardPartRegistry().Register(new DashboardPartRegistration { Id = " ", ComponentType = typeof(PartA) }));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-4, 1)]
    [InlineData(7, 7)]
    [InlineData(99, 12)]
    public void Columns_AreClampedToOneToTwelve(int requested, int expected) =>
        Assert.Equal(expected, DashboardPartRegistration.For<PartA>("a", columns: requested).Columns);

    [Fact]
    public async Task DefaultRegistration_IsFullWidth_AndVisibleToEveryone()
    {
        var registration = DashboardPartRegistration.For<PartA>("a");

        Assert.Equal(12, registration.Columns);
        Assert.Equal(0, registration.Priority);
        Assert.True(await registration.IsVisibleAsync(new DashboardPartContext(new System.Security.Claims.ClaimsPrincipal(), new ServiceCollection().BuildServiceProvider())));
    }

    [Fact]
    public async Task ForRoles_MatchesAnyOfTheRoles_AndNeverWithoutRoles()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var user = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Approver")], "test"));
        var context = new DashboardPartContext(user, services);

        Assert.True(await DashboardVisibility.ForRoles("Admin", "Approver")(context));
        Assert.False(await DashboardVisibility.ForRoles("Admin")(context));
        Assert.False(await DashboardVisibility.ForRoles()(context));
        Assert.True(await DashboardVisibility.ForAnyUser()(context));
        Assert.True(await DashboardVisibility.ForUsers(principal => principal.IsInRole("Approver"))(context));
    }

    [Fact]
    public void AddBlazorBaseDashboard_AddsUpAcrossCalls_ShareOneRegistry()
    {
        var services = new ServiceCollection();
        services.AddBlazorBaseDashboard(options => options.AddPart<PartA>("a"));
        services.AddBlazorBaseDashboard(options => options.AddPart<PartB>("b", priority: -1));
        using var provider = services.BuildServiceProvider();

        var registry = provider.GetRequiredService<IDashboardPartRegistry>();

        Assert.Equal(["b", "a"], registry.Parts.Select(part => part.Id));
        Assert.Same(registry, provider.GetRequiredService<IDashboardPartRegistry>());
    }

    [Fact]
    public void AddBlazorBaseDashboard_RegistersTheSharedServices()
    {
        var services = new ServiceCollection();
        services.AddBlazorBaseDashboard();
        services.AddSingleton(NSubstitute.Substitute.For<Microsoft.JSInterop.IJSRuntime>());
        using var provider = services.BuildServiceProvider();

        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
        Assert.NotNull(provider.GetRequiredService<DashboardClock>());
        Assert.Equal(TimeSpan.FromMinutes(5), provider.GetRequiredService<DashboardSettings>().AutoRefreshInterval);
    }

    [Fact]
    public void AutoRefreshInterval_IsKept_WhenALaterCallDoesNotSetIt()
    {
        var services = new ServiceCollection();
        services.AddBlazorBaseDashboard(options => options.AutoRefreshInterval = TimeSpan.FromMinutes(2));
        services.AddBlazorBaseDashboard(options => options.AddPart<PartA>("a"));
        using var provider = services.BuildServiceProvider();

        Assert.Equal(TimeSpan.FromMinutes(2), provider.GetRequiredService<DashboardSettings>().AutoRefreshInterval);
    }

    [Fact]
    public void NegativeAutoRefreshInterval_MeansOff()
    {
        var services = new ServiceCollection();
        services.AddBlazorBaseDashboard(options => options.AutoRefreshInterval = TimeSpan.FromMinutes(-1));
        using var provider = services.BuildServiceProvider();

        Assert.Equal(TimeSpan.Zero, provider.GetRequiredService<DashboardSettings>().AutoRefreshInterval);
    }

    [Fact]
    public async Task InsightProviders_AreRegisteredScoped_ThroughTheOptionsAndTheExtension()
    {
        var services = new ServiceCollection();
        services.AddBlazorBaseDashboard(options => options.AddHeadlineInsightProvider<CountingInsightProvider>());
        services.AddHeadlineInsightProvider<OtherInsightProvider>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var providers = scope.ServiceProvider.GetServices<IHeadlineInsightProvider>().ToList();

        Assert.Equal(2, providers.Count);
        Assert.Contains(providers, item => item is CountingInsightProvider);
        Assert.Contains(providers, item => item is OtherInsightProvider);
        Assert.NotEmpty(await providers[0].GetInsightsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RefreshService_IsFailSoft_AndRunsAllEntries_AndStopsAfterDispose()
    {
        var service = new DashboardRefreshService();
        var calls = new List<string>();
        using var first = service.Register("a", new DelegateRefreshable(() => throw new InvalidOperationException("boom")));
        var second = service.Register("b", new DelegateRefreshable(() => calls.Add("b")));

        await service.RefreshAllAsync();
        Assert.Equal(["b"], calls);

        await service.RefreshAsync("b", "unknown");
        Assert.Equal(["b", "b"], calls);

        second.Dispose();
        await service.RefreshAllAsync();
        Assert.Equal(["b", "b"], calls);
    }

    [Fact]
    public async Task RefreshAll_JoinsARunningRefresh_AndRaisesCompletedOnce()
    {
        var service = new DashboardRefreshService();
        var gate = new TaskCompletionSource();
        var runs = 0;
        var completed = 0;
        service.RefreshAllCompleted += () => completed++;
        using var registration = service.Register("slow", new DelegateRefreshable(async () => { runs++; await gate.Task; }));

        var first = service.RefreshAllAsync();
        var second = service.RefreshAllAsync();
        gate.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, runs);
        Assert.Equal(1, completed);
    }

    private sealed class OtherInsightProvider : IHeadlineInsightProvider
    {
        public ValueTask<IReadOnlyList<HeadlineInsight>> GetInsightsAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<HeadlineInsight>>([]);
    }

    private sealed class DelegateRefreshable : IDashboardRefreshable
    {
        private readonly Func<Task> Action;

        public DelegateRefreshable(Action action) => Action = () => { action(); return Task.CompletedTask; };

        public DelegateRefreshable(Func<Task> action) => Action = action;

        public Task RefreshAsync() => Action();
    }
}
