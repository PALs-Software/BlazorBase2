using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using BlazorBase.Components.Assistant;
using BlazorBase.Components.Test.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorBase.Components.Test.Assistant;

public sealed class AssistantContextServiceTests : BunitContext
{
    private readonly AssistantClientOptions Settings = new() { SiteId = "dev.acadon" };

    private AssistantContextService CreateService(AuthenticationStateProvider? authentication = null)
    {
        var services = new ServiceCollection();

        if (authentication is not null)
            services.AddSingleton(authentication);

        return new AssistantContextService(
            Services.GetRequiredService<NavigationManager>(),
            Options.Create(Settings),
            services.BuildServiceProvider());
    }

    private void NavigateTo(string path) => Services.GetRequiredService<NavigationManager>().NavigateTo(path);

    private static int SizeOf(AssistantPageContext context) => JsonSerializer.SerializeToUtf8Bytes(context, AssistantJson.Options).Length;

    [Fact]
    public async Task TheContext_HasSiteRouteWithoutQueryAndLocale()
    {
        NavigateTo("budgets/edit?item=42&secret=1#top");
        using var culture = new EnglishCulture();

        var context = await CreateService().CaptureAsync("Budgets - my Cosmos");

        Assert.Equal("dev.acadon", context.SiteId);
        Assert.Equal("/budgets/edit", context.Route);
        Assert.Equal("en", context.Locale);
        Assert.Equal("Budgets - my Cosmos", context.PageTitle);
        Assert.False(context.IsTruncated);
    }

    [Fact]
    public async Task TheExplicitPageTitle_WinsOverTheDocumentTitle()
    {
        var service = CreateService();
        service.PageTitle = "Explicit";

        var context = await service.CaptureAsync("Document");

        Assert.Equal("Explicit", context.PageTitle);
    }

    [Fact]
    public async Task Roles_AreNamesOnlyDistinctAndSorted()
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "Admin"), new Claim("role", "Admin"), new Claim(ClaimTypes.Role, "Boost"), new Claim(ClaimTypes.Email, "a@b.c"), new Claim(ClaimTypes.Name, "Ann")],
            "test");

        var context = await CreateService(new FixedAuthentication(new ClaimsPrincipal(identity))).CaptureAsync();

        Assert.Equal(["Admin", "Boost"], context.Roles);
        Assert.DoesNotContain("a@b.c", JsonSerializer.Serialize(context, AssistantJson.Options));
    }

    [Fact]
    public async Task WithoutAnAuthenticationProvider_RolesAreEmpty()
    {
        var context = await CreateService().CaptureAsync();

        Assert.Empty(context.Roles);
    }

    [Fact]
    public async Task AFailingAuthenticationProvider_DoesNotBreakTheCapture()
    {
        var context = await CreateService(new FailingAuthentication()).CaptureAsync();

        Assert.Empty(context.Roles);
    }

    [Fact]
    public async Task Providers_ContributeAtCaptureTimeAndLaterOnesWin()
    {
        var service = CreateService();
        var list = new Provider(builder =>
        {
            builder.EntityType = "Budget";
            builder.Filters = "2 conditions";
            builder.AddHint("list.sort", "name");
        });
        var card = new Provider(builder =>
        {
            builder.EntityType = "BudgetCard";
            builder.EntityKey = "42";
            builder.Preset = "mine";
        });
        service.Register(list);
        service.Register(card);

        var context = await service.CaptureAsync();

        Assert.Equal("BudgetCard", context.EntityType);
        Assert.Equal("42", context.EntityKey);
        Assert.Equal("2 conditions", context.Filters);
        Assert.Equal("mine", context.Preset);
        Assert.Equal("name", context.Hints["list.sort"]);
    }

    [Fact]
    public async Task ADisposedRegistration_NoLongerContributes()
    {
        var service = CreateService();
        var registration = service.Register(new Provider(builder => builder.EntityType = "Budget"));

        registration.Dispose();
        registration.Dispose();

        Assert.Null((await service.CaptureAsync()).EntityType);
    }

    [Fact]
    public async Task AThrowingProvider_IsSkipped()
    {
        var service = CreateService();
        service.Register(new Provider(_ => throw new InvalidOperationException("boom")));
        service.Register(new Provider(builder => builder.EntityType = "Budget"));

        Assert.Equal("Budget", (await service.CaptureAsync()).EntityType);
    }

    [Fact]
    public async Task ServiceHintsAndTheVisibleError_AreIncludedAndHintsCanBeRemoved()
    {
        var service = CreateService();
        service.SetHint("wizard.step", "3");
        service.SetHint("temp", "x");
        service.RemoveHint("temp");
        service.VisibleError = "The sync failed.";

        var context = await service.CaptureAsync();

        Assert.Equal("3", context.Hints["wizard.step"]);
        Assert.DoesNotContain("temp", context.Hints.Keys);
        Assert.Equal("The sync failed.", context.ErrorText);
    }

    [Fact]
    public async Task ALargeContext_NeverExceedsTheConfiguredSize()
    {
        Settings.MaxContextBytes = 1024;
        var service = CreateService();
        service.VisibleError = new string('e', 5000);
        service.PageTitle = new string('t', 5000);

        for (var index = 0; index < 60; index++)
            service.SetHint($"hint.{index:00}", new string('v', 500));

        var context = await service.CaptureAsync();

        Assert.True(SizeOf(context) <= 1024, $"Size was {SizeOf(context)}.");
        Assert.True(context.IsTruncated);
        Assert.Equal("/", context.Route);
    }

    [Fact]
    public async Task ATinyLimit_DropsValuesUntilTheContextFits()
    {
        Settings.MaxContextBytes = 120;
        var service = CreateService();
        service.PageTitle = "Some title";
        service.VisibleError = "Some error";
        service.SetHint("a", "b");

        var context = await service.CaptureAsync();

        Assert.True(SizeOf(context) <= 120, $"Size was {SizeOf(context)}.");
        Assert.True(context.IsTruncated);
        Assert.Empty(context.Hints);
    }

    [Fact]
    public async Task LongValues_AreShortenedWithoutSplittingASurrogatePair()
    {
        var service = CreateService();
        service.SetHint("emoji", string.Concat(Enumerable.Repeat("😀", 300)));

        var context = await service.CaptureAsync();

        var value = context.Hints["emoji"];
        Assert.True(value.Length <= 200);
        Assert.EndsWith("…", value);
        Assert.False(char.IsHighSurrogate(value[^2]));
        Assert.True(context.IsTruncated);
    }

    [Fact]
    public async Task AContextWithinTheLimit_IsNotMarkedAsTruncated()
    {
        var service = CreateService();
        service.SetHint("a", "b");

        Assert.False((await service.CaptureAsync()).IsTruncated);
    }

    [Fact]
    public async Task TheHintComponent_ContributesWhileItIsRenderedOnly()
    {
        Services.AddSingleton(Options.Create(Settings));
        Services.AddScoped<AssistantContextService>();
        var service = Services.GetRequiredService<AssistantContextService>();

        var cut = Render<AssistantContextHint>(parameters => parameters
            .Add(p => p.Key, "wizard.step")
            .Add(p => p.Value, "3")
            .Add(p => p.EntityType, "Budget")
            .Add(p => p.EntityKey, "7")
            .AddChildContent("<p>content</p>"));

        var context = await service.CaptureAsync();
        Assert.Equal("3", context.Hints["wizard.step"]);
        Assert.Equal("Budget", context.EntityType);
        Assert.Equal("7", context.EntityKey);
        Assert.Contains("content", cut.Markup);

        cut.Instance.Dispose();
        Assert.Null((await service.CaptureAsync()).EntityType);
    }

    [Fact]
    public void TheHintComponent_WorksWithoutTheAssistantRegistered()
    {
        var cut = Render<AssistantContextHint>(parameters => parameters.Add(p => p.Key, "k").Add(p => p.Value, "v"));

        Assert.NotNull(cut.Instance);
    }

    private sealed class Provider(Action<AssistantContextBuilder> contribute) : IAssistantContextProvider
    {
        public void Contribute(AssistantContextBuilder builder) => contribute(builder);
    }

    private sealed class FixedAuthentication(ClaimsPrincipal user) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(user));
    }

    private sealed class FailingAuthentication : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => throw new InvalidOperationException("no auth");
    }
}
