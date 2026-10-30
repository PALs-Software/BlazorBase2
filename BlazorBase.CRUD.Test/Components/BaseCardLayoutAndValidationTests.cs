using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Validation;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using BlazorBase.CRUD.Validation;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

[Collection(CultureCollection.Name)]
public class BaseCardLayoutAndValidationTests : BunitTestContextBase
{
    public BaseCardLayoutAndValidationTests()
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");
        Services.AddLocalization();
    }

    [Fact]
    public void InvalidSubmit_ShowsTheLocalizedMessageNextToTheField_AndDoesNotSave()
    {
        using var _ = UiCulture.Use("de-DE");
        var provider = Substitute.For<IBaseDataProvider<TestProduct>>();

        var cut = RenderCard(new TestProduct { Name = string.Empty }, provider);
        cut.Find("form").Submit();

        var fieldMessage = cut.Find(".base-property-input .base-field-error");
        Assert.Equal("Das Feld „Name“ ist erforderlich.", fieldMessage.TextContent);
        provider.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Fact]
    public void MultilineAndHtmlFields_SpanTheWholeRow()
    {
        var provider = Substitute.For<IBaseDataProvider<TestProduct>>();
        var configuration = new BaseCardBuilder<TestProduct>()
            .Field(p => p.Price)
            .Field(p => p.Description, f => f.Lines(4))
            .Field(p => p.Name, f => f.HtmlField())
            .Build();

        var cut = RenderCard(new TestProduct { Name = "N" }, provider, configuration);

        var fields = cut.FindAll(".base-card-field");
        Assert.Equal(3, fields.Count);
        Assert.DoesNotContain("base-card-field-full", fields[0].ClassName);
        Assert.Contains("base-card-field-full", fields[1].ClassName);
        Assert.Contains("base-card-field-full", fields[2].ClassName);
    }

    [Fact]
    public void HtmlField_IsMarkedFullWidth()
    {
        var field = new BaseCardBuilder<TestProduct>().Field(p => p.Description, f => f.HtmlField()).Build().Fields[0];

        Assert.True(field.FullWidth);
    }

    [Fact]
    public void ColSpanCoveringAllColumns_SpansTheWholeRow()
    {
        var provider = Substitute.For<IBaseDataProvider<TestProduct>>();
        var configuration = new BaseCardBuilder<TestProduct>()
            .Field(p => p.Name, f => f.ColSpan(3))
            .Build();

        var cut = RenderCard(new TestProduct { Name = "N" }, provider, configuration);

        Assert.Contains("base-card-field-full", cut.Find(".base-card-field").ClassName);
    }

    [Fact]
    public void Form_IsNotValidatedNatively_SoTheLocalizedMessagesShow()
    {
        var provider = Substitute.For<IBaseDataProvider<TestProduct>>();

        var cut = RenderCard(new TestProduct { Name = "N" }, provider);

        Assert.True(cut.Find("form").HasAttribute("novalidate"));
    }

    private IRenderedComponent<BaseCard<TestProduct>> RenderCard(
        TestProduct model,
        IBaseDataProvider<TestProduct> provider,
        BaseCardConfiguration<TestProduct>? configuration = null)
    {
        Services.AddSingleton(provider);

        return Render<BaseCard<TestProduct>>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.DataProvider, provider)
            .Add(p => p.IsNew, true)
            .Add(p => p.Configuration, configuration ?? new BaseCardBuilder<TestProduct>().Field(p => p.Name).Build()));
    }
}
