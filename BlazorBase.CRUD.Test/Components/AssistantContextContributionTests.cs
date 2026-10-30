using System.Linq.Expressions;
using BlazorBase.Components.Assistant;
using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>What lists and cards tell the assistant about the page: the entity type and the record key, never field values.</summary>
[Collection(CustomPropertyResolutionCollection.Name)]
public class AssistantContextContributionTests : BunitTestContextBase
{
    private AssistantContextService Assistant => Services.GetRequiredService<AssistantContextService>();

    private void RegisterAssistant()
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");

        Services.AddLocalization();
        Services.AddSingleton(Options.Create(new AssistantClientOptions()));
        Services.AddScoped<AssistantContextService>();
    }

    [Fact]
    public async Task ACard_ReportsItsEntityTypeAndTheKeyButNoFieldValues()
    {
        RegisterAssistant();
        var provider = Substitute.For<IBaseDataProvider<TestStringKeyedNote>>();
        Services.AddSingleton(provider);

        var cut = Render<BaseCard<TestStringKeyedNote>>(parameters => parameters
            .Add(p => p.Model, new TestStringKeyedNote { Id = "note-7", Text = "Confidential text" })
            .Add(p => p.DataProvider, provider)
            .Add(p => p.IsNew, false));

        var context = await Assistant.CaptureAsync();

        Assert.Equal(nameof(TestStringKeyedNote), context.EntityType);
        Assert.Equal("note-7", context.EntityKey);
        Assert.DoesNotContain("Confidential", System.Text.Json.JsonSerializer.Serialize(context));

        await cut.Instance.DisposeAsync();
        Assert.Null((await Assistant.CaptureAsync()).EntityType);
    }

    [Fact]
    public async Task ANewCard_ReportsNoKey()
    {
        RegisterAssistant();
        var provider = Substitute.For<IBaseDataProvider<TestStringKeyedNote>>();
        Services.AddSingleton(provider);

        Render<BaseCard<TestStringKeyedNote>>(parameters => parameters
            .Add(p => p.Model, new TestStringKeyedNote { Text = "Fresh" })
            .Add(p => p.DataProvider, provider)
            .Add(p => p.IsNew, true));

        var context = await Assistant.CaptureAsync();

        Assert.Equal(nameof(TestStringKeyedNote), context.EntityType);
        Assert.Null(context.EntityKey);
    }

    [Fact]
    public async Task AList_ReportsItsEntityType()
    {
        RegisterAssistant();
        var provider = Substitute.For<IBaseDataProvider<TestProduct>>();
        provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestProduct, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new BaseQueryResult<TestProduct> { Items = [new TestProduct { Id = Guid.NewGuid(), Name = "Secret widget" }], TotalCount = 1 });
        Services.AddSingleton(provider);

        var cut = Render<BaseList<TestProduct>>(parameters => parameters
            .Add(p => p.DataProvider, provider)
            .Add(p => p.AllowAdd, false)
            .Add(p => p.ChildContent, (RenderFragment)(builder =>
            {
                builder.OpenComponent<BaseColumn<TestProduct>>(0);
                builder.AddComponentParameter(1, nameof(BaseColumn<TestProduct>.Property), (Expression<Func<TestProduct, object?>>)(product => product.Name));
                builder.CloseComponent();
            })));

        var context = await Assistant.CaptureAsync();

        Assert.Equal(nameof(TestProduct), context.EntityType);
        Assert.DoesNotContain("Secret", System.Text.Json.JsonSerializer.Serialize(context));

        await cut.Instance.DisposeAsync();
        Assert.Null((await Assistant.CaptureAsync()).EntityType);
    }

    [Fact]
    public async Task WithoutTheAssistantRegistered_ListsAndCardsStillRender()
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        Services.AddLocalization();
        var provider = Substitute.For<IBaseDataProvider<TestStringKeyedNote>>();
        Services.AddSingleton(provider);

        var cut = Render<BaseCard<TestStringKeyedNote>>(parameters => parameters
            .Add(p => p.Model, new TestStringKeyedNote { Id = "n" })
            .Add(p => p.DataProvider, provider)
            .Add(p => p.IsNew, false));

        Assert.NotNull(cut.Instance);
        await Task.CompletedTask;
    }
}
