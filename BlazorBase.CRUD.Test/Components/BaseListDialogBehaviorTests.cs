using System.Linq.Expressions;
using BlazorBase.Components.Services;
using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Localization;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// Opening a card from a row opens exactly one dialog, and Escape on a card with unsaved changes asks what the
/// Cancel button asks.
/// </summary>
[Collection(CustomPropertyResolutionCollection.Name)]
public class BaseListDialogBehaviorTests : BunitTestContextBase
{
    /// <summary>
    /// Navigating to an item notifies more than once before the item has even been fetched: once for the click
    /// and once more for the list the router recreates for the new URL. The key has to be claimed before the
    /// fetch, or every notification passes the "already open" check and stacks another identical dialog.
    /// </summary>
    [Fact]
    public async Task OverlappingNavigationsToTheSameItem_OpenExactlyOneDialog()
    {
        var product = new TestProduct { Id = Guid.NewGuid(), Name = "Widget" };
        var dialogService = TestDialogService.Register(this);
        var cut = await RenderListAsync(product);
        var navigation = Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo($"/?item={product.Id}");
        navigation.NavigateTo($"/?item={product.Id}&noise=1");
        navigation.NavigateTo($"/?item={product.Id}&noise=2");

        await WaitUntilAsync(() => dialogService.Shown.Count > 0);
        await Task.Delay(400);
        Assert.Single(dialogService.Shown);
    }

    [Fact]
    public async Task TheCardDialog_IsOpenedWithTheSharedCardDialogDefaults()
    {
        var product = new TestProduct { Id = Guid.NewGuid(), Name = "Widget" };
        var dialogService = TestDialogService.Register(this);
        await RenderListAsync(product);

        Services.GetRequiredService<NavigationManager>().NavigateTo($"/?item={product.Id}");

        await WaitUntilAsync(() => dialogService.Shown.Count > 0);
        var dialog = dialogService.Shown[0];
        Assert.Equal(typeof(BaseDialog<TestProduct>), dialog.Component);
        Assert.Equal(CardDialogDefaults.Width, dialog.Options.Width);
        Assert.Equal(true, dialog.Options.PreventDismissOnEscape);
        Assert.Null(dialog.Options.Header.Title);
        Assert.False(dialog.Options.Header.CloseAction.Visible);
    }

    [Fact]
    public async Task OpeningAnotherItemWhileOneIsOpen_ClosesTheFirstAndOpensTheSecond()
    {
        var first = new TestProduct { Id = Guid.NewGuid(), Name = "First" };
        var second = new TestProduct { Id = Guid.NewGuid(), Name = "Second" };
        var dialogService = TestDialogService.Register(this);
        var cut = await RenderListAsync(first, extraItem: second);
        var navigation = Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo($"/?item={first.Id}");
        await WaitUntilAsync(() => dialogService.Shown.Count == 1);
        navigation.NavigateTo($"/?item={second.Id}");

        await WaitUntilAsync(() => dialogService.Shown.Count == 2);
        Assert.True(dialogService.CardData<TestProduct>()[0].IsCloseRequested);
        Assert.False(dialogService.CardData<TestProduct>()[1].IsCloseRequested);
    }

    [Fact]
    public void EscapeOnACardWithUnsavedChanges_AsksToDiscard_AndKeepsTheCardWhenDeclined()
    {
        var confirmation = Substitute.For<IConfirmationService>();
        confirmation.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(false);
        var cancelled = false;
        var cut = RenderCard(confirmation, () => cancelled = true, edit: note => note.Text = "typed");

        cut.InvokeAsync(() => cut.Instance.RequestCloseAsync()).GetAwaiter().GetResult();

        confirmation.ReceivedWithAnyArgs(1).ConfirmAsync(default!, default!, default!, default!);
        Assert.False(cancelled);
    }

    [Fact]
    public void EscapeOnACardWithUnsavedChanges_ClosesTheCardWhenDiscardIsConfirmed()
    {
        var confirmation = Substitute.For<IConfirmationService>();
        confirmation.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        var cancelled = false;
        var cut = RenderCard(confirmation, () => cancelled = true, edit: note => note.Text = "typed");

        cut.InvokeAsync(() => cut.Instance.RequestCloseAsync()).GetAwaiter().GetResult();

        Assert.True(cancelled);
    }

    [Fact]
    public void EscapeOnAnUntouchedCard_ClosesWithoutAsking()
    {
        var confirmation = Substitute.For<IConfirmationService>();
        var cancelled = false;
        var cut = RenderCard(confirmation, () => cancelled = true, edit: null);

        cut.InvokeAsync(() => cut.Instance.RequestCloseAsync()).GetAwaiter().GetResult();

        confirmation.ReceivedWithAnyArgs(0).ConfirmAsync(default!, default!, default!, default!);
        Assert.True(cancelled);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);

        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(25);

        Assert.True(condition());
    }

    private async Task<IRenderedComponent<BaseList<TestProduct>>> RenderListAsync(TestProduct product, TestProduct? extraItem = null)
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");

        Services.AddLocalization();

        var items = new[] { product, extraItem }.OfType<TestProduct>().ToList();
        var provider = Substitute.For<IBaseDataProvider<TestProduct>>();
        provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestProduct, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new BaseQueryResult<TestProduct> { Items = items, TotalCount = items.Count });
        provider.GetByIdAsync(Arg.Any<object>(), Arg.Any<IEnumerable<string>?>(), Arg.Any<Expression<Func<TestProduct, bool>>?>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await Task.Delay(60);
                return items.FirstOrDefault(item => item.Id.Equals(call.Arg<object>()));
            });
        Services.AddSingleton(provider);

        var cut = Render<BaseList<TestProduct>>(parameters => parameters
            .Add(p => p.DataProvider, provider)
            .Add(p => p.AllowAdd, false)
            .Add(p => p.ChildContent, BuildColumn(item => item.Name)));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tr.base-list-row")));
        await Task.Delay(200);

        return cut;
    }

    private IRenderedComponent<BaseCard<TestStringKeyedNote>> RenderCard(IConfirmationService confirmation, Action onCancelled, Action<TestStringKeyedNote>? edit)
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");

        Services.AddLocalization();
        Services.AddSingleton(confirmation);

        var model = new TestStringKeyedNote { Id = "n1", Text = "original" };
        var provider = Substitute.For<IBaseDataProvider<TestStringKeyedNote>>();
        Services.AddSingleton(provider);

        var cut = Render<BaseCard<TestStringKeyedNote>>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.DataProvider, provider)
            .Add(p => p.IsNew, false)
            .Add(p => p.OnCancelled, EventCallback.Factory.Create(this, onCancelled)));

        if (edit is not null)
            edit(model);

        return cut;
    }

    private static RenderFragment BuildColumn(Expression<Func<TestProduct, object?>> columnProperty)
    {
        return builder =>
        {
            builder.OpenComponent<BaseColumn<TestProduct>>(0);
            builder.AddComponentParameter(1, nameof(BaseColumn<TestProduct>.Property), columnProperty);
            builder.CloseComponent();
        };
    }
}
