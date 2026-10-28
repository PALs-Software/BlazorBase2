using BlazorBase.Components.Assistant;
using BlazorBase.Components.HeaderActions;
using BlazorBase.Components.Layout;
using BlazorBase.Components.Services;
using BlazorBase.Components.Test.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components.Icons.Regular;
using NSubstitute;
using Xunit;

namespace BlazorBase.Components.Test.Assistant;

public sealed class AssistantPanelInLayoutTests : ComponentsBunitTestContextBase
{
    public AssistantPanelInLayoutTests()
    {
        Services.AddLocalization();
        Services.AddSingleton(Substitute.For<IThemeService>());
        Services.AddSingleton(Substitute.For<ILanguageService>());
        Services.AddSingleton<IAssistantChatClient>(new ScriptedAssistantChatClient());
        Services.AddBlazorBaseAssistant(new AssistantClientOptions { SiteId = "test.site" });
        AddAuthorization();
    }

    private static HeaderActionItem ToggleAction() => new()
    {
        Id = "assistant",
        Icon = new Size20.Bug(),
        Title = "Assistant",
        Order = 1,
        OnInvoke = services =>
        {
            services.GetRequiredService<AssistantPanelState>().Toggle();
            return Task.CompletedTask;
        },
    };

    private IRenderedComponent<BaseLayout> RenderLayout() => Render<BaseLayout>(parameters => parameters
        .Add(p => p.AppTitle, "Shell")
        .Add(p => p.HeaderActions, new[] { ToggleAction() })
        .Add(p => p.LayoutOverlays, (RenderFragment)(builder =>
        {
            builder.OpenComponent<BaseAssistantPanel>(0);
            builder.CloseComponent();
        }))
        .AddChildContent("<p id=\"page\">Page</p>"));

    [Fact]
    public void TheClosedPanel_RendersNothingInsideTheLayout()
    {
        var cut = RenderLayout();

        Assert.Empty(cut.FindAll(".assistant-panel"));
    }

    [Fact]
    public void AHeaderAction_TogglesThePanelHostedInTheLayoutOverlays()
    {
        var cut = RenderLayout();

        cut.Find("fluent-button[data-action-id=assistant]").Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".layout-root .main-content .assistant-panel")));
        Assert.NotEmpty(cut.FindAll("#page"));

        cut.Find("fluent-button[data-action-id=assistant]").Click();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".assistant-panel")));
    }

    [Fact]
    public void ThePanelState_OpensThePanelInTheLayoutWithoutTheHeaderAction()
    {
        var cut = RenderLayout();

        Services.GetRequiredService<AssistantPanelState>().Open();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".assistant-panel")));
    }
}
