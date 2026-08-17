using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Localization;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// Whether a model counts as new decides the dialog caption, whether insert or modify rights are
/// checked, and — the part that actually loses data — whether saving creates or updates. These pin
/// the decision down for a key whose unset value is not the CLR default.
/// </summary>
public class BaseCardNewModelTests : BunitTestContextBase
{
    [Fact]
    public void Card_Creates_ForAnUnsavedModelWithAStringKey()
    {
        var provider = RegisterProvider();

        var cut = RenderCard(new TestStringKeyedNote { Text = "Fresh" }, provider);
        cut.Find("form").Submit();

        provider.Received(1).CreateAsync(Arg.Any<TestStringKeyedNote>(), Arg.Any<CancellationToken>());
        provider.DidNotReceiveWithAnyArgs().PatchAsync(default!, default!, default, default);
    }

    [Fact]
    public void Card_DoesNotCreate_ForAModelThatCarriesAStringKey()
    {
        var provider = RegisterProvider();

        var cut = RenderCard(new TestStringKeyedNote { Id = "abc", Text = "Existing" }, provider);
        cut.Find("form").Submit();

        provider.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    /// <summary>
    /// An explicit answer beats the key, so a model that assigns its key before saving still saves
    /// as a creation.
    /// </summary>
    [Fact]
    public void Card_Creates_WhenToldTheModelIsNew_DespiteAFilledKey()
    {
        var provider = RegisterProvider();

        var cut = RenderCard(new TestStringKeyedNote { Id = "assigned-early", Text = "Fresh" }, provider, isNew: true);
        cut.Find("form").Submit();

        provider.Received(1).CreateAsync(Arg.Any<TestStringKeyedNote>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Card_DoesNotCreate_WhenToldTheModelIsNotNew_DespiteAnEmptyKey()
    {
        var provider = RegisterProvider();

        var cut = RenderCard(new TestStringKeyedNote { Text = "Existing" }, provider, isNew: false);
        cut.Find("form").Submit();

        provider.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Fact]
    public void Card_TitlesItselfAdd_ForAnUnsavedModelWithAStringKey()
    {
        var provider = RegisterProvider();
        var framework = Services.GetRequiredService<IStringLocalizerFactory>()
            .Create(typeof(BlazorBaseCrudResources));

        var cut = RenderCard(new TestStringKeyedNote(), provider);

        Assert.Equal(framework["DialogTitleAdd"].Value, cut.Find(".base-card-title").TextContent.Trim());
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

    private IRenderedComponent<BaseCard<TestStringKeyedNote>> RenderCard(
        TestStringKeyedNote model,
        IBaseDataProvider<TestStringKeyedNote> provider,
        bool? isNew = null)
        => Render<BaseCard<TestStringKeyedNote>>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.DataProvider, provider)
            .Add(p => p.IsNew, isNew));
}
