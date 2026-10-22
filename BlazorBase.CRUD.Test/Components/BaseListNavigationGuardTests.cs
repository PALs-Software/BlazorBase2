using System.Linq.Expressions;
using BlazorBase.Components.Services;
using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Components.Internal;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// A navigation that would leave a deep-linked card with unsaved changes (the back button, a link) is held and asks
/// the same question the close button asks; without unsaved changes the card closes as before.
/// </summary>
[Collection(CustomPropertyResolutionCollection.Name)]
public class BaseListNavigationGuardTests : BunitTestContextBase
{
    private readonly TestProduct First = new() { Id = Guid.NewGuid(), Name = "First" };
    private readonly TestProduct Second = new() { Id = Guid.NewGuid(), Name = "Second" };

    [Theory]
    [InlineData("http://localhost/products?item=7", "7", "http://localhost/products", true)]
    [InlineData("http://localhost/products?item=7", "7", "http://localhost/products?item=8", true)]
    [InlineData("http://localhost/products?item=7", "7", "http://localhost/products?item=7", false)]
    [InlineData("http://localhost/products?item=7", "7", "http://localhost/products?item=7&noise=1", false)]
    [InlineData("http://localhost/products?item=7", "7", "http://localhost/other", true)]
    [InlineData("http://localhost/products?item=7", "7", "/products?item=7", false)]
    [InlineData("http://localhost/products?item=7", "7", "/products?item=", true)]
    [InlineData("http://localhost/products?item=7", "7", "https://example.org/", true)]
    public void LeavesOpenItem_ComparesPageAndKey(string currentUri, string openKey, string target, bool expected)
    {
        Assert.Equal(expected, BaseList<TestProduct>.LeavesOpenItem(currentUri, openKey, target, "item"));
    }

    [Fact]
    public void AGuardWithoutACard_ReportsNoChangesAndAllowsClosing()
    {
        var guard = new CardCloseGuard();

        Assert.False(guard.HasUnsavedChanges);
        Assert.True(guard.RequestCloseAsync().GetAwaiter().GetResult());
    }

    [Fact]
    public void AGuard_KeepsTheFirstCardThatRegisters()
    {
        var guard = new CardCloseGuard();
        var outer = new object();
        guard.Register(outer, () => true, () => Task.FromResult(false));
        guard.Register(new object(), () => false, () => Task.FromResult(true));

        Assert.True(guard.HasUnsavedChanges);
        Assert.False(guard.RequestCloseAsync().GetAwaiter().GetResult());

        guard.Unregister(outer);

        Assert.False(guard.HasUnsavedChanges);
    }

    [Fact]
    public void ACardInADialog_RegistersWithTheGuardOfItsDialog()
    {
        var confirmation = Substitute.For<IConfirmationService>();
        confirmation.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(false);
        var guard = new CardCloseGuard();
        var model = new TestStringKeyedNote { Id = "n1", Text = "original" };
        var cut = RenderCard(guard, confirmation, model);

        Assert.False(guard.HasUnsavedChanges);

        model.Text = "typed";

        Assert.True(guard.HasUnsavedChanges);
        Assert.False(cut.InvokeAsync(guard.RequestCloseAsync).GetAwaiter().GetResult());
        confirmation.ReceivedWithAnyArgs(1).ConfirmAsync(default!, default!, default!, default!);
    }

    [Fact]
    public void ACardInADialog_ClosesThroughTheGuardWhenDiscardIsConfirmed()
    {
        var confirmation = Substitute.For<IConfirmationService>();
        confirmation.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        var guard = new CardCloseGuard();
        var model = new TestStringKeyedNote { Id = "n1", Text = "original" };
        var cut = RenderCard(guard, confirmation, model);
        model.Text = "typed";

        var closed = cut.InvokeAsync(guard.RequestCloseAsync).GetAwaiter().GetResult();

        Assert.True(closed);
        Assert.Equal("original", model.Text);
    }

    [Fact]
    public async Task BackWithUnsavedChanges_AsksAndKeepsTheCardOpenWhenTheUserKeepsEditing()
    {
        var session = await OpenCardAsync();
        var asked = 0;
        session.Guard.Register(this, () => true, () =>
        {
            asked++;
            return Task.FromResult(false);
        });

        session.Navigation.NavigateTo("/");
        await Task.Delay(300);

        Assert.Equal(1, asked);
        Assert.Contains($"item={First.Id}", session.Navigation.Uri);
        await session.Reference.DidNotReceive().CloseAsync();
    }

    [Fact]
    public async Task BackWithUnsavedChanges_LeavesTheListWithoutTheParameterWhenTheUserDiscards()
    {
        var session = await OpenCardAsync();
        session.Guard.Register(this, () => true, () =>
        {
            session.CloseTheDialog();
            return Task.FromResult(true);
        });

        session.Navigation.NavigateTo("/");

        await WaitUntilAsync(() => !session.Navigation.Uri.Contains("item="));
    }

    [Fact]
    public async Task LeavingForAnotherItemWithUnsavedChanges_OpensTheOtherItemOnceDiscarded()
    {
        var session = await OpenCardAsync(withSecond: true);
        session.Guard.Register(this, () => true, () =>
        {
            session.CloseTheDialog();
            return Task.FromResult(true);
        });

        session.Navigation.NavigateTo($"/?item={Second.Id}");

        await WaitUntilAsync(() => session.DialogRequests == 2);
        Assert.Contains($"item={Second.Id}", session.Navigation.Uri);
    }

    [Fact]
    public async Task BackWithoutUnsavedChanges_ClosesTheCardWithoutAsking()
    {
        var session = await OpenCardAsync();
        var asked = 0;
        session.Guard.Register(this, () => false, () =>
        {
            asked++;
            return Task.FromResult(true);
        });

        session.Navigation.NavigateTo("/");
        await Task.Delay(300);

        Assert.Equal(0, asked);
        Assert.DoesNotContain("item=", session.Navigation.Uri);
        await session.Reference.Received(1).CloseAsync();
    }

    [Fact]
    public async Task ANavigationThatStaysOnTheOpenItem_IsNotHeld()
    {
        var session = await OpenCardAsync();
        var asked = 0;
        session.Guard.Register(this, () => true, () =>
        {
            asked++;
            return Task.FromResult(false);
        });

        session.Navigation.NavigateTo($"/?item={First.Id}&noise=1");
        await Task.Delay(300);

        Assert.Equal(0, asked);
        Assert.Contains("noise=1", session.Navigation.Uri);
    }

    private async Task<Session> OpenCardAsync(bool withSecond = false)
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");

        Services.AddLocalization();

        var session = new Session();
        var dialogService = Substitute.For<IDialogService>();
        dialogService.ShowDialogAsync<BaseDialog<TestProduct>>(Arg.Any<object>(), Arg.Any<DialogParameters>())
            .Returns(call =>
            {
                session.DialogRequests++;
                session.Opened = new TaskCompletionSource<DialogResult>();
                var reference = Substitute.For<IDialogReference>();
                reference.Result.Returns(session.Opened.Task);
                reference.CloseAsync().Returns(_ =>
                {
                    session.CloseTheDialog();
                    return Task.CompletedTask;
                });
                session.Reference = reference;
                session.Guard = ((BaseDialogData<TestProduct>)call.ArgAt<object>(0)).CloseGuard;
                return Task.FromResult(reference);
            });
        Services.AddSingleton(dialogService);

        var items = withSecond ? new List<TestProduct> { First, Second } : [First];
        var provider = Substitute.For<IBaseDataProvider<TestProduct>>();
        provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestProduct, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new BaseQueryResult<TestProduct> { Items = items, TotalCount = items.Count });
        provider.GetByIdAsync(Arg.Any<object>(), Arg.Any<IEnumerable<string>?>(), Arg.Any<Expression<Func<TestProduct, bool>>?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(items.FirstOrDefault(item => item.Id.Equals(call.Arg<object>()))));
        Services.AddSingleton(provider);

        var cut = Render<BaseList<TestProduct>>(parameters => parameters
            .Add(p => p.DataProvider, provider)
            .Add(p => p.AllowAdd, false)
            .Add(p => p.ChildContent, BuildColumn(item => item.Name)));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tr.fluent-data-grid-row")));
        await Task.Delay(200);

        session.Navigation = Services.GetRequiredService<NavigationManager>();
        session.Navigation.NavigateTo($"/?item={First.Id}");
        await WaitUntilAsync(() => session.DialogRequests == 1 && session.Guard is not null);
        await Task.Delay(100);

        return session;
    }

    private IRenderedComponent<BaseCard<TestStringKeyedNote>> RenderCard(CardCloseGuard guard, IConfirmationService confirmation, TestStringKeyedNote model)
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");

        Services.AddLocalization();
        Services.AddSingleton(confirmation);

        var provider = Substitute.For<IBaseDataProvider<TestStringKeyedNote>>();
        Services.AddSingleton(provider);

        return Render<BaseCard<TestStringKeyedNote>>(parameters => parameters
            .AddCascadingValue(guard)
            .Add(p => p.Model, model)
            .Add(p => p.DataProvider, provider)
            .Add(p => p.IsNew, false));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);

        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(25);

        Assert.True(condition());
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

    private sealed class Session
    {
        public int DialogRequests { get; set; }

        public TaskCompletionSource<DialogResult> Opened { get; set; } = new();

        public IDialogReference Reference { get; set; } = default!;

        public CardCloseGuard Guard { get; set; } = default!;

        public NavigationManager Navigation { get; set; } = default!;

        public void CloseTheDialog() => Opened.TrySetResult(DialogResult.Cancel());
    }
}
