using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using BlazorBase.CRUD.Test.Validation;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// A field that cannot be changed inside an editable card looks read-only at first sight; a card that is read-only
/// as a whole keeps its calm look.
/// </summary>
[Collection(CultureCollection.Name)]
public class BaseCardReadOnlyFieldTests : BunitTestContextBase
{
    private const string ReadOnlyClass = "base-property-input-readonly";

    public BaseCardReadOnlyFieldTests()
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");
        Services.AddLocalization();
    }

    [Fact]
    public void NonEditableField_InAnEditableCard_CarriesTheReadOnlyMarker()
    {
        var cut = RenderCard(readOnlyCard: false);

        var readOnlyInput = InputOf(cut, "Price");
        Assert.Contains(ReadOnlyClass, readOnlyInput.ClassName);
        Assert.Equal("true", readOnlyInput.GetAttribute("data-readonly"));
    }

    [Fact]
    public void EditableField_InAnEditableCard_DoesNotCarryTheReadOnlyMarker()
    {
        var cut = RenderCard(readOnlyCard: false);

        var editableInput = InputOf(cut, "Name");
        Assert.DoesNotContain(ReadOnlyClass, editableInput.ClassName);
        Assert.False(editableInput.HasAttribute("data-readonly"));
    }

    [Fact]
    public void EveryFieldType_OfAnEditableCard_MarksItsNonEditableVariant()
    {
        var provider = Substitute.For<IBaseDataProvider<TestProduct>>();
        var configuration = new BaseCardBuilder<TestProduct>()
            .Field(p => p.Name, f => f.Editable(false))
            .Field(p => p.Price, f => f.Editable(false))
            .Field(p => p.IsActive, f => f.Editable(false))
            .Field(p => p.ReleasedOn, f => f.Editable(false))
            .Field(p => p.Kind, f => f.Editable(false))
            .Field(p => p.Description, f => f.Editable(false).Lines(3))
            .Build();

        var cut = RenderCard(readOnlyCard: false, configuration, provider);

        Assert.Equal(6, cut.FindAll(".base-property-input").Count);
        Assert.Equal(6, cut.FindAll("." + ReadOnlyClass).Count);
    }

    [Fact]
    public void ReadOnlyCard_KeepsItsCalmLook_WithoutTheMarker()
    {
        var cut = RenderCard(readOnlyCard: true);

        Assert.NotEmpty(cut.FindAll(".base-property-input"));
        Assert.Empty(cut.FindAll("." + ReadOnlyClass));
        Assert.Empty(cut.FindAll("[data-readonly]"));
    }

    private static AngleSharp.Dom.IElement InputOf(IRenderedComponent<BaseCard<TestProduct>> cut, string fieldName)
    {
        var index = fieldName == nameof(TestProduct.Name) ? 0 : 1;
        return cut.FindAll(".base-card-field")[index].QuerySelector(".base-property-input")!;
    }

    private IRenderedComponent<BaseCard<TestProduct>> RenderCard(
        bool readOnlyCard,
        BaseCardConfiguration<TestProduct>? configuration = null,
        IBaseDataProvider<TestProduct>? provider = null)
    {
        provider ??= Substitute.For<IBaseDataProvider<TestProduct>>();
        Services.AddSingleton(provider);

        configuration ??= new BaseCardBuilder<TestProduct>()
            .Field(p => p.Name)
            .Field(p => p.Price, f => f.Editable(false))
            .Build();

        return Render<BaseCard<TestProduct>>(parameters => parameters
            .Add(p => p.Model, new TestProduct { Name = "N" })
            .Add(p => p.DataProvider, provider)
            .Add(p => p.IsNew, true)
            .Add(p => p.ReadOnly, readOnlyCard)
            .Add(p => p.Configuration, configuration));
    }
}
