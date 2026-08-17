using System.Linq.Expressions;
using BlazorBase.CRUD.Components.Internal;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// Covers CRUD-UI-02: a navigation-lookup field must not query or expose entities of a target type
/// the current user has no class-level Read right on.
/// </summary>
[Collection(CustomPropertyResolutionCollection.Name)]
public class BasePropertyInputNavigationAccessTests : BunitTestContextBase
{
    [Fact]
    public void NavigationProperty_UserWithoutReadOnTargetType_DoesNotQueryOrExposeTargetItems()
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("regular-user");
        authorization.SetRoles("User");

        var targetProvider = Substitute.For<IBaseDataProvider<NavigationAccessTarget>>();
        targetProvider.GetCountAsync(Arg.Any<Expression<Func<NavigationAccessTarget, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(1);
        targetProvider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<NavigationAccessTarget, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new BaseQueryResult<NavigationAccessTarget>
            {
                Items = [new NavigationAccessTarget { Id = Guid.NewGuid(), Name = "Secret Target" }],
                TotalCount = 1
            });
        Services.AddSingleton(targetProvider);

        var field = new BaseCardBuilder<NavigationAccessOwner>().Field(o => o.Target).Build().Fields[0];

        var cut = Render<BasePropertyInput<NavigationAccessOwner>>(parameters => parameters
            .Add(p => p.Model, new NavigationAccessOwner())
            .Add(p => p.FieldConfig, field)
            .Add(p => p.IsEditing, true));

        targetProvider.DidNotReceive().GetCountAsync(Arg.Any<Expression<Func<NavigationAccessTarget, bool>>>(), Arg.Any<CancellationToken>());
        targetProvider.DidNotReceive().GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<NavigationAccessTarget, bool>>>(), Arg.Any<CancellationToken>());
        Assert.DoesNotContain("Secret Target", cut.Markup);
    }

    [Fact]
    public void NavigationProperty_UserWithoutReadOnTargetType_TargetPrePopulated_ShowsReadOnlyBoundDisplayText()
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("regular-user");
        authorization.SetRoles("User");

        var targetProvider = Substitute.For<IBaseDataProvider<NavigationAccessTarget>>();
        targetProvider.GetCountAsync(Arg.Any<Expression<Func<NavigationAccessTarget, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(1);
        targetProvider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<NavigationAccessTarget, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new BaseQueryResult<NavigationAccessTarget>
            {
                Items = [new NavigationAccessTarget { Id = Guid.NewGuid(), Name = "Secret Target" }],
                TotalCount = 1
            });
        Services.AddSingleton(targetProvider);

        var targetId = Guid.NewGuid();
        var owner = new NavigationAccessOwner
        {
            TargetId = targetId,
            Target = new NavigationAccessTarget { Id = targetId, Name = "Bound Target" }
        };

        var field = new BaseCardBuilder<NavigationAccessOwner>().Field(o => o.Target).Build().Fields[0];

        var cut = Render<BasePropertyInput<NavigationAccessOwner>>(parameters => parameters
            .Add(p => p.Model, owner)
            .Add(p => p.FieldConfig, field)
            .Add(p => p.IsEditing, true));

        targetProvider.DidNotReceive().GetCountAsync(Arg.Any<Expression<Func<NavigationAccessTarget, bool>>>(), Arg.Any<CancellationToken>());
        targetProvider.DidNotReceive().GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<NavigationAccessTarget, bool>>>(), Arg.Any<CancellationToken>());
        Assert.DoesNotContain("Secret Target", cut.Markup);

        var textField = cut.Find("fluent-text-field");
        Assert.True(textField.HasAttribute("readonly"));
        Assert.Equal("Bound Target", textField.GetAttribute("value"));
    }

    [Fact]
    public void NavigationProperty_UserWithReadOnTargetType_LoadsLookupItems()
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin-user");
        authorization.SetRoles("Admin");

        var targetProvider = Substitute.For<IBaseDataProvider<NavigationAccessTarget>>();
        targetProvider.GetCountAsync(Arg.Any<Expression<Func<NavigationAccessTarget, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(1);
        targetProvider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<NavigationAccessTarget, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new BaseQueryResult<NavigationAccessTarget>
            {
                Items = [new NavigationAccessTarget { Id = Guid.NewGuid(), Name = "Visible Target" }],
                TotalCount = 1
            });
        Services.AddSingleton(targetProvider);

        var field = new BaseCardBuilder<NavigationAccessOwner>().Field(o => o.Target).Build().Fields[0];

        var cut = Render<BasePropertyInput<NavigationAccessOwner>>(parameters => parameters
            .Add(p => p.Model, new NavigationAccessOwner())
            .Add(p => p.FieldConfig, field)
            .Add(p => p.IsEditing, true));

        targetProvider.Received(1).GetCountAsync(Arg.Any<Expression<Func<NavigationAccessTarget, bool>>>(), Arg.Any<CancellationToken>());
        Assert.Contains("Visible Target", cut.Markup);
    }
}
