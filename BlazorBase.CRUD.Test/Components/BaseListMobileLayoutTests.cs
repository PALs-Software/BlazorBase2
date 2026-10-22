using System.Linq.Expressions;
using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Localization;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// The list on a narrow screen: tiles instead of the table, with the table's interactions (opening an item, the
/// more menu, selection, sorting, paging) kept.
/// </summary>
[Collection(CustomPropertyResolutionCollection.Name)]
public class BaseListMobileLayoutTests : BunitTestContextBase
{
    private const string MobileModulePath = "./_content/BlazorBase.CRUD/js/baseListMobile.js";

    [Fact]
    public void Always_RendersATilePerItem_AndNoTable()
    {
        var cut = RenderList(BaseListMobileLayout.Always);

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-list-card").Count));
        Assert.Empty(cut.FindAll("tr.fluent-data-grid-row"));
        Assert.Empty(cut.FindAll("fluent-data-grid"));
    }

    [Fact]
    public void Never_KeepsTheTable()
    {
        var cut = RenderList(BaseListMobileLayout.Never);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tr.fluent-data-grid-row")));
        Assert.Empty(cut.FindAll(".base-list-card"));
    }

    [Fact]
    public void Auto_KeepsTheTable_WhileTheContainerIsWide()
    {
        var cut = RenderList(BaseListMobileLayout.Auto);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tr.fluent-data-grid-row")));
        Assert.Empty(cut.FindAll(".base-list-card"));
    }

    [Fact]
    public void Auto_ShowsTiles_WhenTheLayoutScriptReportsANarrowContainer()
    {
        var module = JSInterop.SetupModule(MobileModulePath);
        module.Setup<bool>("observeLayout", _ => true).SetResult(true);

        var cut = RenderList(BaseListMobileLayout.Auto);

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-list-card").Count));
        Assert.Empty(cut.FindAll("tr.fluent-data-grid-row"));
        Assert.Equal(MobileCardPlanResolver.DefaultBreakpoint, (int)module.Invocations["observeLayout"].Single().Arguments[2]!);
    }

    [Fact]
    public void Breakpoint_ComesFromTheParameterBeforeTheConfiguration()
    {
        var module = JSInterop.SetupModule(MobileModulePath);
        module.Setup<bool>("observeLayout", _ => true).SetResult(false);

        RenderList(BaseListMobileLayout.Auto, breakpoint: 480, configuration: new BaseListConfiguration<TestProduct> { MobileBreakpoint = 900 });

        Assert.Equal(480, (int)module.Invocations["observeLayout"].Single().Arguments[2]!);
    }

    [Fact]
    public void Breakpoint_ComesFromTheConfigurationBeforeTheDefault()
    {
        var module = JSInterop.SetupModule(MobileModulePath);
        module.Setup<bool>("observeLayout", _ => true).SetResult(false);

        RenderList(BaseListMobileLayout.Auto, configuration: new BaseListConfiguration<TestProduct> { MobileBreakpoint = 900 });

        Assert.Equal(900, (int)module.Invocations["observeLayout"].Single().Arguments[2]!);
    }

    [Fact]
    public void ZeroBreakpoint_DoesNotObserveTheLayoutAtAll()
    {
        var module = JSInterop.SetupModule(MobileModulePath);

        var cut = RenderList(BaseListMobileLayout.Auto, breakpoint: 0);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tr.fluent-data-grid-row")));
        Assert.DoesNotContain(JSInterop.Invocations, invocation => invocation.Identifier == "observeLayout");
    }

    [Fact]
    public async Task CrossingTheBreakpoint_SwitchesBetweenTilesAndTable()
    {
        var module = JSInterop.SetupModule(MobileModulePath);
        module.Setup<bool>("observeLayout", _ => true).SetResult(false);
        var cut = RenderList(BaseListMobileLayout.Auto);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tr.fluent-data-grid-row")));

        await cut.InvokeAsync(() => cut.Instance.OnMobileLayoutChangedAsync(true));

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-list-card").Count));
        Assert.Empty(cut.FindAll("tr.fluent-data-grid-row"));

        await cut.InvokeAsync(() => cut.Instance.OnMobileLayoutChangedAsync(false));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tr.fluent-data-grid-row")));
        Assert.Empty(cut.FindAll(".base-list-card"));
    }

    [Fact]
    public void Tile_ShowsTitleSubtitleBadgesAndLabelledFields()
    {
        var cut = RenderList(BaseListMobileLayout.Always);

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-list-card").Count));
        var tile = cut.FindAll(".base-list-card")[0];

        Assert.Equal("Widget", tile.QuerySelector(".base-list-card-title")!.TextContent.Trim());
        Assert.Equal("A fine widget", tile.QuerySelector(".base-list-card-subtitle")!.TextContent.Trim());
        var badges = tile.QuerySelectorAll(".base-list-card-badge").Select(badge => badge.TextContent.Trim()).ToList();
        Assert.Equal(["IsActive", "Digital"], badges);
        Assert.Contains(tile.QuerySelectorAll(".base-list-card-label"), label => label.TextContent.Trim() == "Price");
    }

    [Fact]
    public void Tile_ShowsABooleanBadgeThatIsFalse_AsAnOffBadgeWithTheSameLabel()
    {
        var cut = RenderList(BaseListMobileLayout.Always);

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-list-card").Count));
        var active = cut.FindAll(".base-list-card")[0].QuerySelectorAll(".base-list-card-badge").First(badge => badge.TextContent.Trim() == "IsActive");
        var inactive = cut.FindAll(".base-list-card")[1].QuerySelectorAll(".base-list-card-badge").First(badge => badge.TextContent.Trim() == "IsActive");

        Assert.Contains("is-flag", active.ClassList);
        Assert.DoesNotContain("is-off", active.ClassList);
        Assert.Equal(Framework["BoolTrue"].Value, active.QuerySelector(".base-list-card-badge-icon")!.GetAttribute("aria-label"));
        Assert.Contains("is-flag", inactive.ClassList);
        Assert.Contains("is-off", inactive.ClassList);
        Assert.Equal(Framework["BoolFalse"].Value, inactive.QuerySelector(".base-list-card-badge-icon")!.GetAttribute("aria-label"));
        Assert.NotNull(inactive.QuerySelector(".base-list-card-badge-icon svg"));
    }

    [Fact]
    public void Tile_ShowsADashInsteadOfAnEmptySubtitle()
    {
        var cut = RenderList(BaseListMobileLayout.Always);

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-list-card").Count));
        var subtitle = cut.FindAll(".base-list-card")[1].QuerySelector(".base-list-card-subtitle")!;

        Assert.Equal("–", subtitle.TextContent.Trim());
        Assert.Equal(Framework["TileNoValue"].Value, subtitle.QuerySelector(".base-list-card-empty")!.GetAttribute("aria-label"));
    }

    [Fact]
    public void Tile_ShowsTheLabelOfAFieldWithoutValue_AndADashAsItsValue()
    {
        var configuration = new MobileCardBuilder<TestProduct>().Title(p => p.Name).Field(p => p.Description).Field(p => p.Stock).Build();

        var cut = RenderList(BaseListMobileLayout.Always, mobileCard: configuration);

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-list-card").Count));
        var tile = cut.FindAll(".base-list-card")[1];
        var fields = tile.QuerySelectorAll(".base-list-card-field").ToList();

        Assert.Equal(2, fields.Count);
        Assert.Equal("Description", fields[0].QuerySelector(".base-list-card-label")!.TextContent.Trim());
        Assert.Equal("–", fields[0].QuerySelector(".base-list-card-value")!.TextContent.Trim());
        Assert.Equal("1", fields[1].QuerySelector(".base-list-card-value")!.TextContent.Trim());
    }

    [Fact]
    public void Tile_ShowsADashInTheBadgeRow_WhenNoBadgeHasAValue()
    {
        var configuration = new MobileCardBuilder<TestProduct>().Title(p => p.Name).Badge(p => p.Description).Build();

        var cut = RenderList(BaseListMobileLayout.Always, mobileCard: configuration);

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-list-card").Count));
        var row = cut.FindAll(".base-list-card")[1].QuerySelector(".base-list-card-badges")!;

        Assert.Empty(row.QuerySelectorAll(".base-list-card-badge"));
        Assert.Equal("–", row.TextContent.Trim());
    }

    [Fact]
    public void EveryTile_RendersEveryPartOfThePlan_WhateverTheValues()
    {
        var cut = RenderList(BaseListMobileLayout.Always);

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-list-card").Count));

        Assert.All(cut.FindAll(".base-list-card"), tile =>
        {
            var parts = tile.QuerySelector(".base-list-card-body")!.Children.Select(part => part.ClassList.First()).ToList();
            Assert.Equal(["base-list-card-title", "base-list-card-subtitle", "base-list-card-badges", "base-list-card-fields"], parts);
        });
    }

    [Fact]
    public void TheEntryCount_IsHiddenOnTheTileLayout()
    {
        var cut = RenderList(BaseListMobileLayout.Always);
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-list-card").Count));

        Assert.Empty(cut.FindAll(".base-list-count"));
    }

    [Fact]
    public void TheEntryCount_StaysOnTheTableLayout()
    {
        var cut = RenderList(BaseListMobileLayout.Never);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tr.fluent-data-grid-row")));

        Assert.NotEmpty(cut.FindAll(".base-list-count"));
    }

    [Fact]
    public async Task TheEntryCount_ComesBackWhenTheListLeavesTheTileLayout()
    {
        var module = JSInterop.SetupModule(MobileModulePath);
        module.Setup<bool>("observeLayout", _ => true).SetResult(true);
        var cut = RenderList(BaseListMobileLayout.Auto);
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-list-card").Count));
        Assert.Empty(cut.FindAll(".base-list-count"));

        await cut.InvokeAsync(() => cut.Instance.OnMobileLayoutChangedAsync(false));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-list-count")));
    }

    [Fact]
    public void Tile_ShowsOnlyTheFieldsOfTheTileConfiguration()
    {
        var configuration = new MobileCardBuilder<TestProduct>().Title(p => p.Name).Field(p => p.Stock).Build();

        var cut = RenderList(BaseListMobileLayout.Always, mobileCard: configuration);

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-list-card").Count));
        var tile = cut.FindAll(".base-list-card")[0];

        Assert.Equal(["Stock"], tile.QuerySelectorAll(".base-list-card-label").Select(label => label.TextContent.Trim()));
        Assert.Empty(tile.QuerySelectorAll(".base-list-card-badge"));
        Assert.Null(tile.QuerySelector(".base-list-card-subtitle"));
    }

    [Fact]
    public void Tile_CustomTemplateReplacesTheBody_ButKeepsTheMoreButton()
    {
        RenderFragment<TestProduct> template = product => builder => builder.AddMarkupContent(0, $"<span class=\"custom\">{product.Name}!</span>");

        var cut = RenderList(BaseListMobileLayout.Always, mobileTemplate: template);

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-list-card").Count));
        Assert.Equal("Widget!", cut.FindAll(".base-list-card")[0].QuerySelector(".custom")!.TextContent);
        Assert.Empty(cut.FindAll(".base-list-card-title"));
        Assert.Equal(2, cut.FindAll(".base-list-card-more").Count);
    }

    [Fact]
    public void Tile_RendersTheTemplateOfAnActionsColumnAsTheActionRow_NotAsAField()
    {
        var cut = RenderList(BaseListMobileLayout.Always, extraColumns: builder =>
        {
            builder.OpenComponent<BaseColumn<TestProduct>>(10);
            builder.AddComponentParameter(11, nameof(BaseColumn<TestProduct>.Property), (Expression<Func<TestProduct, object?>>)(p => p.Id));
            builder.AddComponentParameter(12, nameof(BaseColumn<TestProduct>.MobileRole), MobileRole.Actions);
            builder.AddComponentParameter(13, nameof(BaseColumn<TestProduct>.Template), (RenderFragment<TestProduct>)(product => b => b.AddMarkupContent(0, $"<button class=\"row-action\">Run {product.Name}</button>")));
            builder.CloseComponent();
        });

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-list-card").Count));
        var tile = cut.FindAll(".base-list-card")[0];

        Assert.Equal("Run Widget", tile.QuerySelector(".base-list-card-actions .row-action")!.TextContent);
        Assert.DoesNotContain(tile.QuerySelectorAll(".base-list-card-label"), label => label.TextContent.Trim() == "Id");
    }

    [Fact]
    public void TappingATile_ReportsTheItemLikeARowClick()
    {
        TestProduct? selected = null;
        var cut = RenderList(BaseListMobileLayout.Always, onItemSelected: item => selected = item);
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-list-card").Count));

        cut.FindAll(".base-list-card-body")[0].Click();

        Assert.Equal("Widget", selected?.Name);
    }

    [Fact]
    public void ATileThatDoesNothing_IsNotAButton()
    {
        var cut = RenderList(BaseListMobileLayout.Always);

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-list-card").Count));

        Assert.Empty(cut.FindAll("button.base-list-card-body"));
        Assert.Empty(cut.FindAll(".is-interactive"));
    }

    [Fact]
    public void ATileThatOpens_IsAKeyboardReachableButton()
    {
        var cut = RenderList(BaseListMobileLayout.Always, onItemSelected: _ => { });

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("button.base-list-card-body.is-interactive").Count));
    }

    [Fact]
    public void MoreButton_OpensTheSameMenuAsTheTableRow()
    {
        var cut = RenderList(BaseListMobileLayout.Always);
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-list-card-more").Count));

        cut.FindAll(".base-list-card-more")[0].Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".context-menu")));
        Assert.Equal("menu", cut.Find(".context-menu").GetAttribute("role"));
    }

    [Fact]
    public void ContextMenuEvent_OnATile_OpensTheMenuToo()
    {
        var cut = RenderList(BaseListMobileLayout.Always);
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-list-card").Count));

        cut.FindAll(".base-list-card")[0].ContextMenu(new MouseEventArgs { ClientX = 20, ClientY = 20 });

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".context-menu")));
    }

    [Fact]
    public void SelectionMarks_AppearOnlyOnceAnItemIsSelected_AndTappingThenTogglesTheSelection()
    {
        TestProduct? opened = null;
        var cut = RenderList(BaseListMobileLayout.Always, onItemSelected: item => opened = item, allowDelete: true);
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-list-card-more").Count));
        Assert.Empty(cut.FindAll(".base-list-card-check"));

        cut.FindAll(".base-list-card-more")[0].Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".context-menu")));
        cut.FindAll(".context-menu [role=\"menuitem\"]").First(item => item.TextContent.Trim() == Framework["Select"].Value).Click();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-list-card-check").Count));
        Assert.Single(cut.FindAll(".base-list-card.is-selected"));

        cut.FindAll(".base-list-card-body")[1].Click();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-list-card.is-selected").Count));
        Assert.Null(opened);
    }

    [Fact]
    public void SortSelector_SortsOnTheProvider_AndTheDirectionButtonFlipsIt()
    {
        var provider = Provider(Products());
        var cut = RenderList(BaseListMobileLayout.Always, provider: provider);
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-list-card").Count));
        Assert.True(cut.Find(".base-list-card-sort-direction").HasAttribute("disabled"));

        var view = cut.FindComponent<BaseListCardView<TestProduct>>().Instance.View;
        cut.InvokeAsync(() => view.OnSortChanged(nameof(TestProduct.Price), false));

        cut.WaitForAssertion(() =>
        {
            var sort = Assert.Single(provider.ReceivedQueries<TestProduct>().Last().Sorts);
            Assert.Equal(nameof(TestProduct.Price), sort.PropertyName);
            Assert.Equal(SortDirection.Ascending, sort.Direction);
            Assert.False(cut.Find(".base-list-card-sort-direction").HasAttribute("disabled"));
        });

        cut.Find(".base-list-card-sort-direction").Click();

        cut.WaitForAssertion(() =>
            Assert.Equal(SortDirection.Descending, Assert.Single(provider.ReceivedQueries<TestProduct>().Last().Sorts).Direction));
    }

    [Fact]
    public void SortSelector_OffersTheSortableColumnsAndTheDefaultOrder()
    {
        var cut = RenderList(BaseListMobileLayout.Always);
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-list-card").Count));

        var options = cut.FindAll("fluent-select.base-list-card-sort fluent-option").Select(option => option.TextContent.Trim()).ToList();

        Assert.Equal(Framework["SortDefault"].Value, options[0]);
        Assert.Contains("Name", options);
        Assert.Contains("Price", options);
    }

    [Fact]
    public void Tile_ShowsADashForATemplatedFieldWhosePropertyIsNull()
    {
        var withCategory = Guid.NewGuid();
        var provider = Provider(
        [
            new() { Id = Guid.NewGuid(), Name = "With", CategoryId = withCategory },
            new() { Id = Guid.NewGuid(), Name = "Without", CategoryId = null }
        ]);

        var cut = RenderList(BaseListMobileLayout.Always, provider: provider, extraColumns: builder =>
        {
            builder.OpenComponent<BaseColumn<TestProduct>>(10);
            builder.AddComponentParameter(11, nameof(BaseColumn<TestProduct>.Property), (Expression<Func<TestProduct, object?>>)(p => p.CategoryId));
            builder.AddComponentParameter(12, nameof(BaseColumn<TestProduct>.Title), "Category");
            builder.AddComponentParameter(13, nameof(BaseColumn<TestProduct>.MobileRole), MobileRole.Field);
            builder.AddComponentParameter(14, nameof(BaseColumn<TestProduct>.Template), (RenderFragment<TestProduct>)(product => b => b.AddContent(0, "Shoes")));
            builder.CloseComponent();
        });

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-list-card").Count));
        var values = cut.FindAll(".base-list-card")
            .Select(tile => tile.QuerySelectorAll(".base-list-card-field").First(field => field.QuerySelector(".base-list-card-label")!.TextContent.Trim() == "Category").QuerySelector(".base-list-card-value")!.TextContent.Trim())
            .ToList();

        Assert.Equal(["Shoes", "–"], values);
    }

    [Fact]
    public void ATileWithoutAnyTitleValue_FallsBackToTheDisplayName()
    {
        var provider = Provider([new() { Id = Guid.NewGuid(), Name = String.Empty, Description = "only a description" }]);

        var cut = RenderList(BaseListMobileLayout.Always, provider: provider);

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".base-list-card")));
        Assert.False(String.IsNullOrWhiteSpace(cut.Find(".base-list-card-title").TextContent));
    }

    [Fact]
    public void PageSize_OnlySizesTheFetches_SoEveryTileStillRenders()
    {
        var cut = RenderList(BaseListMobileLayout.Always, pageSize: 1);

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-list-card").Count));
        Assert.Empty(cut.FindAll(".base-list-load-more"));
    }

    [Fact]
    public void EveryTile_HasTheHeightThePlanSaysInsideItsSlot()
    {
        var cut = RenderList(BaseListMobileLayout.Always);

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-list-card").Count));

        Assert.Equal(2, cut.FindAll("li.base-list-tile").Count);
        Assert.All(cut.FindAll("li.base-list-tile"), slot => Assert.NotNull(slot.QuerySelector(":scope > .base-list-card")));
        Assert.Contains("--base-list-tile-height: calc(", cut.Find(".base-list-cards").GetAttribute("style"));
    }

    [Fact]
    public void ATileWithACustomBody_UsesTheHeightTheListWasGiven()
    {
        var cut = RenderList(BaseListMobileLayout.Always, mobileTemplate: product => builder => builder.AddContent(0, product.Name), mobileItemHeight: 210);

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-list-card").Count));

        Assert.Contains("calc(13rem + 2px)", cut.Find(".base-list-cards").GetAttribute("style"));
    }

    [Fact]
    public void EmptyList_ShowsTheEmptyMessageInsteadOfTiles()
    {
        var cut = RenderList(BaseListMobileLayout.Always, provider: Provider([]));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".base-list-empty")));
        Assert.Empty(cut.FindAll(".base-list-card"));
    }

    [Fact]
    public void MoreButton_IsLabelledAndTheTileListIsAList()
    {
        var cut = RenderList(BaseListMobileLayout.Always);
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-list-card").Count));

        Assert.Equal(Framework["MoreActions"].Value, cut.FindAll(".base-list-card-more")[0].GetAttribute("aria-label"));
        Assert.Equal("UL", cut.Find(".base-list-cards").TagName);
    }

    private IStringLocalizer Framework => Services.GetRequiredService<IStringLocalizerFactory>()
        .Create(typeof(BlazorBaseCrudResources));

    private static List<TestProduct> Products() =>
    [
        new() { Id = Guid.NewGuid(), Name = "Widget", Description = "A fine widget", IsActive = true, Kind = ProductKind.Digital, Price = 10m, Stock = 3 },
        new() { Id = Guid.NewGuid(), Name = "Gadget", Description = null, IsActive = false, Kind = ProductKind.Physical, Price = 5m, Stock = 1 }
    ];

    private static IBaseDataProvider<TestProduct> Provider(List<TestProduct> all)
    {
        var provider = Substitute.For<IBaseDataProvider<TestProduct>>();
        provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestProduct, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var query = call.Arg<BaseQuery>();
                IEnumerable<TestProduct> items = all;

                foreach (var sort in query.Sorts.AsEnumerable().Reverse())
                {
                    var property = typeof(TestProduct).GetProperty(sort.PropertyName)!;
                    items = sort.Direction == SortDirection.Descending
                        ? items.OrderByDescending(item => property.GetValue(item))
                        : items.OrderBy(item => property.GetValue(item));
                }

                return new BaseQueryResult<TestProduct> { Items = [.. items.Skip(query.Skip).Take(query.Take)], TotalCount = all.Count };
            });
        return provider;
    }

    private IRenderedComponent<BaseList<TestProduct>> RenderList(
        BaseListMobileLayout layout,
        int? breakpoint = null,
        BaseListConfiguration<TestProduct>? configuration = null,
        MobileCardConfiguration<TestProduct>? mobileCard = null,
        RenderFragment<TestProduct>? mobileTemplate = null,
        Action<RenderTreeBuilder>? extraColumns = null,
        Action<TestProduct>? onItemSelected = null,
        IBaseDataProvider<TestProduct>? provider = null,
        int pageSize = 50,
        bool allowDelete = false,
        int? mobileItemHeight = null)
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");
        Services.AddLocalization();

        provider ??= Provider(Products());
        Services.AddSingleton(provider);

        return Render<BaseList<TestProduct>>(parameters =>
        {
            parameters
                .Add(p => p.DataProvider, provider)
                .Add(p => p.AllowAdd, false)
                .Add(p => p.AllowEdit, false)
                .Add(p => p.AllowDelete, allowDelete)
                .Add(p => p.PageSize, pageSize)
                .Add(p => p.MobileLayout, layout)
                .Add(p => p.MobileBreakpoint, breakpoint)
                .Add(p => p.Configuration, configuration)
                .Add(p => p.MobileCard, mobileCard)
                .Add(p => p.MobileTemplate, mobileTemplate)
                .Add(p => p.MobileItemHeight, mobileItemHeight)
                .Add(p => p.ChildContent, BuildColumns(extraColumns));

            if (onItemSelected is not null)
                parameters.Add(p => p.OnItemSelected, EventCallback.Factory.Create<TestProduct>(this, onItemSelected));
        });
    }

    private static RenderFragment BuildColumns(Action<RenderTreeBuilder>? extraColumns)
    {
        return builder =>
        {
            AddColumn(builder, 0, product => product.Name);
            AddColumn(builder, 3, product => product.Description);
            AddColumn(builder, 6, product => product.Price);
            AddColumn(builder, 9, product => product.Stock);
            AddColumn(builder, 12, product => product.IsActive);
            AddColumn(builder, 15, product => product.Kind);
            extraColumns?.Invoke(builder);
        };
    }

    private static void AddColumn(RenderTreeBuilder builder, int sequence, Expression<Func<TestProduct, object?>> property)
    {
        builder.OpenComponent<BaseColumn<TestProduct>>(sequence);
        builder.AddComponentParameter(sequence + 1, nameof(BaseColumn<TestProduct>.Property), property);
        builder.CloseComponent();
    }
}
