using System.Globalization;
using System.Linq.Expressions;
using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Configuration;
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
/// A list that has neither a card configuration nor a card type still opens a card: one built from its visible columns,
/// read-only when the user may not edit.
/// </summary>
[Collection(CustomPropertyResolutionCollection.Name)]
public class BaseListFallbackCardTests : BunitTestContextBase
{
    private const string InteropModulePath = "./_content/BlazorBase.CRUD/js/baseListInterop.js";
    private const string RowSelector = "tr.base-list-row:not([row-type])";

    private TestDialogService? dialogs;

    private List<BaseDialogData<TestProduct>> openedCards => dialogs?.CardData<TestProduct>() ?? [];
    private readonly IBaseDataProvider<TestProduct> provider = Substitute.For<IBaseDataProvider<TestProduct>>();

    public BaseListFallbackCardTests() => Services.AddLocalization();

    [Fact]
    public void ReadOnlyList_WithoutACardDefinition_OpensAReadOnlyCardWithOneFieldPerVisibleColumn()
    {
        var cut = RenderList(allowEdit: false);

        OpenFirstRow(cut);

        var card = Assert.Single(openedCards);
        Assert.True(card.ReadOnly);
        Assert.NotNull(card.CardConfiguration);
        Assert.Equal(["Name", "Price", "ReleasedOn", "IsActive", "Kind", "Description", "Category"],
            card.CardConfiguration.Fields.Select(field => field.PropertyName));
        Assert.Equal(["Name"], card.CardConfiguration.DisplayKeys.Select(key => key.PropertyName));
    }

    [Fact]
    public void ColumnThatTheUserMayNotRead_IsNotOnTheCard()
    {
        var cut = RenderList(allowEdit: false);

        OpenFirstRow(cut);

        Assert.DoesNotContain(openedCards[0].CardConfiguration!.Fields, field => field.PropertyName == "InternalNotes");
    }

    [Fact]
    public void ListWithAnExplicitCardConfiguration_KeepsUsingIt()
    {
        var explicitConfiguration = new BaseCardConfiguration<TestProduct>();
        var cut = RenderList(allowEdit: false, cardConfiguration: explicitConfiguration);

        OpenFirstRow(cut);

        Assert.Same(explicitConfiguration, Assert.Single(openedCards).CardConfiguration);
    }

    [Fact]
    public void FieldsTakeTheTitleOfTheirColumn()
    {
        var cut = RenderList(allowEdit: false);

        OpenFirstRow(cut);

        var name = openedCards[0].CardConfiguration!.Fields.First(field => field.PropertyName == "Name");
        Assert.Equal("Product name", name.Label);
    }

    [Fact]
    public void EditableList_WithoutACardDefinition_OpensAnEditCardWithEditableFieldsExceptTheKey()
    {
        var cut = RenderList(allowEdit: true, includeId: true);

        OpenFirstRow(cut);

        var card = Assert.Single(openedCards);
        Assert.False(card.ReadOnly);
        var fields = card.CardConfiguration!.Fields;
        Assert.False(fields.First(field => field.PropertyName == "Id").Editable);
        Assert.True(fields.First(field => field.PropertyName == "Name").Editable);
        Assert.False(fields.First(field => field.PropertyName == "Category").Editable);
    }

    [Fact]
    public void FormattedDateAndNestedColumns_ShowTheTextTheListShows_InsteadOfAnInput()
    {
        var cut = RenderList(allowEdit: false);

        OpenFirstRow(cut);

        var fields = openedCards[0].CardConfiguration!.Fields;
        Assert.NotNull(fields.First(field => field.PropertyName == "Price").DisplayTemplate);
        Assert.NotNull(fields.First(field => field.PropertyName == "ReleasedOn").DisplayTemplate);
        Assert.NotNull(fields.First(field => field.PropertyName == "Category").DisplayTemplate);
        Assert.Null(fields.First(field => field.PropertyName == "Name").DisplayTemplate);
        Assert.Null(fields.First(field => field.PropertyName == "IsActive").DisplayTemplate);
    }

    [Fact]
    public void LongText_GetsAFullWidthMultiLineField_AndShortTextDoesNot()
    {
        var cut = RenderList(allowEdit: false);

        OpenFirstRow(cut);

        var fields = openedCards[0].CardConfiguration!.Fields;
        var description = fields.First(field => field.PropertyName == "Description");
        Assert.True(description.FullWidth);
        Assert.Equal(5, description.Lines);
        Assert.False(fields.First(field => field.PropertyName == "Name").FullWidth);
    }

    [Fact]
    public void TheFallbackCard_RendersLabelsAndValuesAndNothingToEdit()
    {
        var cut = RenderList(allowEdit: false);
        OpenFirstRow(cut);
        var card = openedCards[0];

        var rendered = Render<BaseCard<TestProduct>>(parameters => parameters
            .Add(p => p.Model, card.Model)
            .Add(p => p.IsNew, false)
            .Add(p => p.DataProvider, provider)
            .Add(p => p.ReadOnly, true)
            .Add(p => p.Configuration, card.CardConfiguration));

        var text = rendered.Markup;
        Assert.Contains("Product name", text);
        Assert.Contains("Widget", text);
        Assert.Contains(1234.5m.ToString("N2", CultureInfo.CurrentCulture), text);
        Assert.DoesNotContain("InternalNotes", text);
        Assert.DoesNotContain("secret", text);
        Assert.Empty(rendered.FindAll("button[type=submit]"));
        Assert.Empty(rendered.FindAll(".base-card-actions"));
    }

    [Fact]
    public void ACardTypeWithoutAReadOnlyParameter_StillOpensNothingWithoutEditRights()
    {
        var cut = RenderList(allowEdit: false, cardType: typeof(WrappingNoteCardStub));

        Assert.Empty(cut.FindAll("td.base-list-open-cell"));
        Assert.Empty(openedCards);
    }

    private sealed class WrappingNoteCardStub : ComponentBase;

    private static void OpenFirstRow(IRenderedComponent<BaseList<TestProduct>> cut)
    {
        cut.WaitForState(() => cut.FindAll(RowSelector).Count > 0);
        cut.FindAll(RowSelector)[0].DoubleClick();
    }

    private IRenderedComponent<BaseList<TestProduct>> RenderList(
        bool allowEdit,
        bool includeId = false,
        BaseCardConfiguration<TestProduct>? cardConfiguration = null,
        Type? cardType = null)
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");

        var module = JSInterop.SetupModule(InteropModulePath);
        module.Setup<bool>("isPointOnDataRow", _ => true).SetResult(true);
        module.SetupVoid("rememberFocus", _ => true).SetVoidResult();
        module.SetupVoid("restoreFocus", _ => true).SetVoidResult();

        var product = new TestProduct
        {
            Id = Guid.NewGuid(),
            Name = "Widget",
            Price = 1234.5m,
            ReleasedOn = new DateTime(2025, 3, 4, 13, 45, 0, DateTimeKind.Utc),
            IsActive = true,
            Kind = ProductKind.Physical,
            Description = new string('x', 200),
            InternalNotes = "secret",
            Category = new TestCategory { Name = "Tools" }
        };

        provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestProduct, bool>>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new BaseQueryResult<TestProduct> { Items = [product], TotalCount = 1 }));
        provider.GetByIdAsync(Arg.Any<object>(), Arg.Any<IEnumerable<string>?>(), Arg.Any<Expression<Func<TestProduct, bool>>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<TestProduct?>(product));

        dialogs = TestDialogService.Register(this, DialogResult.Cancel());
        Services.AddSingleton(provider);

        var cut = Render<BaseList<TestProduct>>(parameters => parameters
            .Add(p => p.DataProvider, provider)
            .Add(p => p.AllowAdd, false)
            .Add(p => p.AllowEdit, allowEdit)
            .Add(p => p.EnableDeepLinking, false)
            .Add(p => p.CardConfiguration, cardConfiguration)
            .Add(p => p.CardType, cardType)
            .Add(p => p.ChildContent, BuildColumns(includeId)));

        cut.WaitForState(() => cut.FindAll(RowSelector).Count > 0);

        return cut;
    }

    private static RenderFragment BuildColumns(bool includeId) => builder =>
    {
        var sequence = 0;

        void Column(Expression<Func<TestProduct, object?>> property, string? title = null, string? format = null)
        {
            builder.OpenComponent<BaseColumn<TestProduct>>(sequence++);
            builder.AddComponentParameter(sequence++, nameof(BaseColumn<TestProduct>.Property), property);
            builder.AddComponentParameter(sequence++, nameof(BaseColumn<TestProduct>.Title), title);
            builder.AddComponentParameter(sequence++, nameof(BaseColumn<TestProduct>.Format), format);
            builder.CloseComponent();
        }

        if (includeId)
            Column(p => p.Id);

        Column(p => p.Name, "Product name");
        Column(p => p.Price, format: "N2");
        Column(p => p.ReleasedOn);
        Column(p => p.IsActive);
        Column(p => p.Kind);
        Column(p => p.Description);
        Column(p => p.Category!.Name);
        Column(p => p.InternalNotes);
    };
}
