using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Localization;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.Cards;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// A card reached through the <c>CardType</c> seam is instantiated by <c>DynamicComponent</c> and
/// only receives the parameters it declares itself, so the dialog's answer to "is this model new"
/// cannot travel as a parameter. It is cascaded instead. Without that, an entity assigning its key
/// up front — the normal shape for a sequential GUID — fell back to the key heuristic, looked like
/// an existing row, and saved a creation as an update against a row that was never inserted.
/// </summary>
public class CustomCardIsNewTests : BunitTestContextBase
{
    [Fact]
    public void CustomCard_Creates_WhenTheDialogCascadesThatTheModelIsNew_DespiteAFilledKey()
    {
        var provider = RegisterProvider();

        var cut = RenderWrappedCard(new TestStringKeyedNote { Id = "assigned-early", Text = "Fresh" }, isNew: true);
        cut.Find("form").Submit();

        provider.Received(1).CreateAsync(Arg.Any<TestStringKeyedNote>(), Arg.Any<CancellationToken>());
        provider.DidNotReceiveWithAnyArgs().PatchAsync(default!, default!, default, default);
    }

    [Fact]
    public void CustomCard_DoesNotCreate_WhenTheDialogCascadesThatTheModelIsNotNew_DespiteAnEmptyKey()
    {
        var provider = RegisterProvider();

        var cut = RenderWrappedCard(new TestStringKeyedNote { Text = "Existing" }, isNew: false);
        cut.Find("form").Submit();

        provider.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Fact]
    public void CustomCard_FallsBackToTheKey_WhenNothingIsCascaded()
    {
        var provider = RegisterProvider();

        var cut = RenderUncascadedCard(new TestStringKeyedNote { Text = "Fresh" });
        cut.Find("form").Submit();

        provider.Received(1).CreateAsync(Arg.Any<TestStringKeyedNote>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The same answer also captions the dialog, so an empty key must not title itself "add" once the
    /// dialog has said the row already exists.
    /// </summary>
    [Fact]
    public void CustomCard_TitlesItselfEdit_WhenTheDialogCascadesThatTheModelIsNotNew()
    {
        RegisterProvider();
        var framework = Services.GetRequiredService<IStringLocalizerFactory>()
            .Create(typeof(BlazorBaseCrudResources));

        var cut = RenderWrappedCard(new TestStringKeyedNote(), isNew: false);

        Assert.Equal(framework["DialogTitleEdit"].Value, cut.Find(".base-card-title").TextContent.Trim());
    }

    private IBaseDataProvider<TestStringKeyedNote> RegisterProvider()
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");

        Services.AddLocalization();

        var provider = Substitute.For<IBaseDataProvider<TestStringKeyedNote>>();
        provider.CreateAsync(Arg.Any<TestStringKeyedNote>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(callInfo.Arg<TestStringKeyedNote>()));
        provider.PatchAsync(Arg.Any<object>(), Arg.Any<Dictionary<string, object?>>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new TestStringKeyedNote()));

        Services.AddSingleton(provider);

        return provider;
    }

    private IRenderedComponent<WrappingNoteCard> RenderWrappedCard(TestStringKeyedNote model, bool isNew)
    {
        RenderTree.Add<CascadingValue<bool?>>(parameters => parameters
            .Add(cascade => cascade.Name, CardCascadeNames.IsNew)
            .Add(cascade => cascade.Value, isNew));

        return RenderUncascadedCard(model);
    }

    private IRenderedComponent<WrappingNoteCard> RenderUncascadedCard(TestStringKeyedNote model)
        => Render<WrappingNoteCard>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.DataProvider, Services.GetRequiredService<IBaseDataProvider<TestStringKeyedNote>>()));
}
