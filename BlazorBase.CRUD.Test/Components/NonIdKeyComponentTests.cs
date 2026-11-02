using System.Linq.Expressions;
using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// An entity whose key is not named <c>Id</c> is saved, deleted and queried by its key: the card patches the row it
/// shows instead of an empty id, the list deletes by the key and always selects it.
/// </summary>
public class NonIdKeyComponentTests : BunitTestContextBase
{
    [Fact]
    public void Card_PatchesTheRowByItsKey()
    {
        var provider = RegisterProvider();
        var model = new TestTypeNameKeyed { TestTypeNameKeyedId = Guid.NewGuid(), Name = "Before" };
        var cut = Render<BaseCard<TestTypeNameKeyed>>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.DataProvider, provider));

        model.Name = "After";
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => provider.Received(1).PatchAsync(
            model.TestTypeNameKeyedId, Arg.Is<Dictionary<string, object?>>(fields => fields.ContainsKey(nameof(TestTypeNameKeyed.Name))),
            Arg.Any<string?>(), Arg.Any<CancellationToken>()));
        provider.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Fact]
    public void Card_TreatsAnEmptyKeyAsANewRow()
    {
        var provider = RegisterProvider();
        var cut = Render<BaseCard<TestTypeNameKeyed>>(parameters => parameters
            .Add(p => p.Model, new TestTypeNameKeyed { Name = "Fresh" })
            .Add(p => p.DataProvider, provider));

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => provider.Received(1).CreateAsync(Arg.Any<TestTypeNameKeyed>(), Arg.Any<CancellationToken>()));
    }

    [Fact]
    public async Task List_DeletesTheRowByItsKey()
    {
        var row = new TestTypeNameKeyed { TestTypeNameKeyedId = Guid.NewGuid(), Name = "Row" };
        var provider = RegisterProvider();
        provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestTypeNameKeyed, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new BaseQueryResult<TestTypeNameKeyed> { Items = [row], TotalCount = 1 });
        var cut = RenderList(provider);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tr.base-list-row")));

        await cut.InvokeAsync(() => (Task)typeof(BaseList<TestTypeNameKeyed>)
            .GetMethod("OnDeleteItemAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(cut.Instance, [row])!);

        await provider.Received(1).DeleteAsync(row.TestTypeNameKeyedId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void List_AlwaysSelectsTheKey()
    {
        var provider = RegisterProvider();
        provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestTypeNameKeyed, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new BaseQueryResult<TestTypeNameKeyed> { Items = [], TotalCount = 0 });

        RenderList(provider);

        provider.Received().GetListAsync(
            Arg.Is<BaseQuery>(query => query.Select != null && query.Select.Contains(nameof(TestTypeNameKeyed.TestTypeNameKeyedId))),
            Arg.Any<Expression<Func<TestTypeNameKeyed, bool>>>(), Arg.Any<CancellationToken>());
    }

    private IBaseDataProvider<TestTypeNameKeyed> RegisterProvider()
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");

        Services.AddLocalization();

        var provider = Substitute.For<IBaseDataProvider<TestTypeNameKeyed>>();
        provider.CreateAsync(Arg.Any<TestTypeNameKeyed>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.Arg<TestTypeNameKeyed>()));
        provider.PatchAsync(Arg.Any<object>(), Arg.Any<Dictionary<string, object?>>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new TestTypeNameKeyed()));
        Services.AddSingleton(provider);

        return provider;
    }

    private IRenderedComponent<BaseList<TestTypeNameKeyed>> RenderList(IBaseDataProvider<TestTypeNameKeyed> provider)
        => Render<BaseList<TestTypeNameKeyed>>(parameters => parameters
            .Add(p => p.DataProvider, provider)
            .Add(p => p.AllowAdd, false)
            .Add(p => p.ChildContent, (RenderFragment)(builder =>
            {
                builder.OpenComponent<BaseColumn<TestTypeNameKeyed>>(0);
                builder.AddComponentParameter(1, nameof(BaseColumn<TestTypeNameKeyed>.Property),
                    (Expression<Func<TestTypeNameKeyed, object?>>)(row => row.Name));
                builder.CloseComponent();
            })));
}
