using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Net;
using System.Text.Json.Serialization;
using BlazorBase.CRUD.Attributes;
using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Components.Internal;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Localization;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// What a save does with the items of a list part, and what the card does when the save fails. Creating a parent
/// posts the items along with it, so creating them a second time made the server reject the duplicate while the
/// parent already existed; a failed request must stay inside the card instead of taking the application down.
/// </summary>
[Collection(CustomPropertyResolutionCollection.Name)]
public class BaseCardSaveTests : BunitTestContextBase
{
    [Fact]
    public void CreatingAParent_DoesNotPostItemsThatTravelWithIt()
    {
        var parentProvider = RegisterProvider<SerializedParent>();
        var childProvider = RegisterProvider<SerializedChild>();
        var cut = RenderCard(new SerializedParent { Name = "Parent" }, parentProvider, ListPart<SerializedParent>(parent => parent.Children));

        AddListPartItem<SerializedParent>(cut);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => parentProvider.Received(1).CreateAsync(Arg.Any<SerializedParent>(), Arg.Any<CancellationToken>()));
        childProvider.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Fact]
    public void CreatingAParent_PostsItemsThatAreNotPartOfItsPayload()
    {
        var parentProvider = RegisterProvider<IgnoringParent>();
        var childProvider = RegisterProvider<IgnoringChild>();
        var cut = RenderCard(new IgnoringParent { Name = "Parent" }, parentProvider, ListPart<IgnoringParent>(parent => parent.Children));

        AddListPartItem<IgnoringParent>(cut);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => childProvider.Received(1).CreateAsync(Arg.Any<IgnoringChild>(), Arg.Any<CancellationToken>()));
    }

    [Fact]
    public void AFailedRequest_StaysInsideTheCard()
    {
        var framework = Services.AddLocalization().BuildServiceProvider().GetRequiredService<IStringLocalizerFactory>().Create(typeof(BlazorBaseCrudResources));
        var provider = RegisterProvider<TestStringKeyedNote>();
        provider.CreateAsync(Arg.Any<TestStringKeyedNote>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("failure", null, HttpStatusCode.InternalServerError));
        var saved = false;

        var cut = Render<BaseCard<TestStringKeyedNote>>(parameters => parameters
            .Add(p => p.Model, new TestStringKeyedNote { Text = "Fresh" })
            .Add(p => p.DataProvider, provider)
            .Add(p => p.IsNew, true)
            .Add(p => p.OnAfterSave, Microsoft.AspNetCore.Components.EventCallback.Factory.Create<TestStringKeyedNote>(this, _ => saved = true)));
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains(framework["SaveFailed", "HTTP 500"].Value, cut.Markup));
        Assert.False(saved);
        Assert.NotNull(cut.Find("form"));
    }

    [Fact]
    public void AnUnhandledExceptionInTheContent_IsShownAndKeepsTheContent()
    {
        var cut = Render<CardErrorBoundary>(parameters => parameters
            .Add(p => p.ChildContent, (RenderFragment)(builder =>
            {
                builder.OpenComponent<ThrowingButton>(0);
                builder.CloseComponent();
            }))
            .Add(p => p.ErrorContent, exception => (RenderFragment)(builder => builder.AddContent(0, $"failed: {exception.Message}"))));

        cut.Find("button").Click();

        Assert.Contains("failed: boom", cut.Markup);
        Assert.NotNull(cut.Find("button"));
    }

    [Fact]
    public void AnUnsavedCard_KeepsItsAddCaption_WhileTheKeyIsTyped()
    {
        var framework = Services.AddLocalization().BuildServiceProvider().GetRequiredService<IStringLocalizerFactory>().Create(typeof(BlazorBaseCrudResources));
        var provider = RegisterProvider<TestStringKeyedNote>();

        var cut = Render<BaseCard<TestStringKeyedNote>>(parameters => parameters
            .Add(p => p.Model, new TestStringKeyedNote { Id = "typed-key", Text = "Fresh" })
            .Add(p => p.DataProvider, provider)
            .Add(p => p.IsNew, true));

        Assert.Equal(framework["DialogTitleAdd"].Value, cut.Find(".base-card-title").TextContent.Trim());
    }

    [Fact]
    public void ACollectionIgnoredByJson_IsNotSerializedWithItsParent()
    {
        Assert.True(BaseCard<SerializedParent>.IsSerializedWithParent(typeof(SerializedParent).GetProperty(nameof(SerializedParent.Children))!));
        Assert.False(BaseCard<IgnoringParent>.IsSerializedWithParent(typeof(IgnoringParent).GetProperty(nameof(IgnoringParent.Children))!));
    }

    private static void AddListPartItem<TModel>(IRenderedComponent<BaseCard<TModel>> cut)
        where TModel : class, new()
    {
        cut.Find(".base-listpart-header fluent-button").Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".base-listpart-item")));
    }

    private static RenderFragment ListPart<TModel>(Expression<Func<TModel, object?>> property)
        where TModel : class
        => builder =>
        {
            builder.OpenComponent<ListPartField<TModel>>(0);
            builder.AddAttribute(1, nameof(ListPartField<TModel>.Property), property);
            builder.CloseComponent();
        };

    private IBaseDataProvider<TModel> RegisterProvider<TModel>()
        where TModel : class, new()
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");

        Services.AddLocalization();

        var provider = Substitute.For<IBaseDataProvider<TModel>>();
        provider.CreateAsync(Arg.Any<TModel>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.Arg<TModel>()));
        Services.AddSingleton(provider);

        var dialogService = Substitute.For<IDialogService>();
        var reference = Substitute.For<IDialogReference>();
        reference.Result.Returns(Task.FromResult(DialogResult.Ok(new Dictionary<string, object?>())));
        dialogService.ShowDialogAsync<BaseListPartItemDialog<SerializedChild>>(Arg.Any<object>(), Arg.Any<DialogParameters>()).Returns(Task.FromResult(reference));
        dialogService.ShowDialogAsync<BaseListPartItemDialog<IgnoringChild>>(Arg.Any<object>(), Arg.Any<DialogParameters>()).Returns(Task.FromResult(reference));
        Services.AddSingleton(dialogService);

        return provider;
    }

    private IRenderedComponent<BaseCard<TModel>> RenderCard<TModel>(TModel model, IBaseDataProvider<TModel> provider, RenderFragment content)
        where TModel : class, new()
        => Render<BaseCard<TModel>>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.DataProvider, provider)
            .Add(p => p.IsNew, true)
            .Add(p => p.ChildContent, content));

    [CrudAccess("Admin", "RIMD")]
    public class SerializedParent
    {
        public Guid Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public ICollection<SerializedChild> Children { get; set; } = [];
    }

    [CrudAccess("Admin", "RIMD")]
    public class SerializedChild
    {
        public Guid Id { get; set; }

        public Guid SerializedParentId { get; set; }

        public string Title { get; set; } = string.Empty;
    }

    [CrudAccess("Admin", "RIMD")]
    public class IgnoringParent
    {
        public Guid Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [JsonIgnore]
        public ICollection<IgnoringChild> Children { get; set; } = [];
    }

    [CrudAccess("Admin", "RIMD")]
    public class IgnoringChild
    {
        public Guid Id { get; set; }

        public Guid IgnoringParentId { get; set; }

        public string Title { get; set; } = string.Empty;
    }

    private sealed class ThrowingButton : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, "onclick", EventCallback.Factory.Create(this, () => throw new InvalidOperationException("boom")));
            builder.AddContent(2, "go");
            builder.CloseElement();
        }
    }
}
