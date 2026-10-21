using BlazorBase.Components.Services;
using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// A card shown on a page without a list or a dialog has nothing to close: the close button must not
/// appear there, and cancelling reverts the edits instead. Inside a dialog or with a consumer for the
/// close request nothing changes.
/// </summary>
public class BaseCardStandaloneTests : BunitTestContextBase
{
    private readonly IConfirmationService confirmation = Substitute.For<IConfirmationService>();

    [Fact]
    public void AStandaloneCard_HasNoCloseButton()
    {
        var cut = RenderCard();

        Assert.Empty(cut.FindAll(".base-card-close"));
    }

    [Fact]
    public void AStandaloneCard_IsMarkedInline_SoItFillsThePage()
    {
        var cut = RenderCard();

        Assert.NotNull(cut.Find(".base-card.base-card-inline"));
    }

    [Fact]
    public void ACardWithACloseConsumer_ShowsTheCloseButton()
    {
        var cut = RenderCard(onCancelled: EventCallback.Factory.Create(this, () => { }));

        Assert.Single(cut.FindAll(".base-card-close"));
    }

    [Fact]
    public void ShowCloseButton_ForcesTheButtonOnAStandaloneCard()
    {
        var cut = RenderCard(showCloseButton: true);

        Assert.Single(cut.FindAll(".base-card-close"));
    }

    [Fact]
    public void ShowCloseButton_HidesTheButtonOnACardWithAConsumer()
    {
        var cut = RenderCard(onCancelled: EventCallback.Factory.Create(this, () => { }), showCloseButton: false);

        Assert.Empty(cut.FindAll(".base-card-close"));
    }

    [Fact]
    public void CancellingAStandaloneCard_RevertsTheEdits_AfterTheUserConfirms()
    {
        confirmation.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        var model = new TestStringKeyedNote { Id = "n1", Text = "Original" };
        var cut = RenderCard(model);

        model.Text = "Edited";
        ClickCancel(cut);

        cut.WaitForAssertion(() => Assert.Equal("Original", model.Text));
        confirmation.ReceivedWithAnyArgs(1).ConfirmAsync(default!, default!, default!, default!);
    }

    [Fact]
    public void CancellingAStandaloneCard_KeepsTheEdits_WhenTheUserKeepsEditing()
    {
        confirmation.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(false);
        var model = new TestStringKeyedNote { Id = "n1", Text = "Original" };
        var cut = RenderCard(model);

        model.Text = "Edited";
        ClickCancel(cut);

        cut.WaitForAssertion(() => confirmation.ReceivedWithAnyArgs(1).ConfirmAsync(default!, default!, default!, default!));
        Assert.Equal("Edited", model.Text);
    }

    [Fact]
    public void CancellingACleanStandaloneCard_DoesNotAsk()
    {
        var cut = RenderCard(new TestStringKeyedNote { Id = "n1", Text = "Original" });

        ClickCancel(cut);

        confirmation.DidNotReceiveWithAnyArgs().ConfirmAsync(default!, default!, default!, default!);
    }

    [Fact]
    public void ADeferredSave_MakesTheSavedValuesTheNewBaseline()
    {
        var model = new TestStringKeyedNote { Id = "n1", Text = "Original" };
        var cut = Render<BaseCard<TestStringKeyedNote>>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.DataProvider, RegisterProvider())
            .Add(p => p.DeferSave, true)
            .Add(p => p.OnDeferredSave, EventCallback.Factory.Create<Dictionary<string, object?>>(this, _ => { })));

        model.Text = "Saved";
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("form")));
        ClickCancel(cut);

        confirmation.DidNotReceiveWithAnyArgs().ConfirmAsync(default!, default!, default!, default!);
        Assert.Equal("Saved", model.Text);
    }

    [Fact]
    public void ANewModelFromTheParent_BecomesTheBaseline()
    {
        var provider = RegisterProvider();
        var cut = Render<BaseCard<TestStringKeyedNote>>(parameters => parameters
            .Add(p => p.Model, new TestStringKeyedNote { Id = "n1", Text = "Original" })
            .Add(p => p.DataProvider, provider));

        var replacement = new TestStringKeyedNote { Id = "n1", Text = "Reloaded" };
        cut.Render(parameters => parameters
            .Add(p => p.Model, replacement)
            .Add(p => p.DataProvider, provider));
        ClickCancel(cut);

        confirmation.DidNotReceiveWithAnyArgs().ConfirmAsync(default!, default!, default!, default!);
        Assert.Equal("Reloaded", replacement.Text);
    }

    private static void ClickCancel(IRenderedComponent<BaseCard<TestStringKeyedNote>> cut) =>
        cut.FindAll(".base-card-actions fluent-button")[^1].Click();

    private IBaseDataProvider<TestStringKeyedNote> RegisterProvider()
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");

        Services.AddLocalization();
        Services.AddSingleton(confirmation);

        var provider = Substitute.For<IBaseDataProvider<TestStringKeyedNote>>();
        Services.AddSingleton(provider);

        return provider;
    }

    private IRenderedComponent<BaseCard<TestStringKeyedNote>> RenderCard(
        TestStringKeyedNote? model = null,
        EventCallback onCancelled = default,
        bool? showCloseButton = null)
    {
        var provider = RegisterProvider();

        return Render<BaseCard<TestStringKeyedNote>>(parameters => parameters
            .Add(p => p.Model, model ?? new TestStringKeyedNote { Id = "n1", Text = "Original" })
            .Add(p => p.DataProvider, provider)
            .Add(p => p.OnCancelled, onCancelled)
            .Add(p => p.ShowCloseButton, showCloseButton));
    }
}
