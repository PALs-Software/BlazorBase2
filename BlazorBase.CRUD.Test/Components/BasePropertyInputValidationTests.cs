using System.Linq.Expressions;
using BlazorBase.CRUD.Components.Internal;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Validation;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using BlazorBase.CRUD.Validation;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

[Collection(CultureCollection.Name)]
public class BasePropertyInputValidationTests : BunitTestContextBase
{
    public BasePropertyInputValidationTests() => Services.AddLocalization();

    [Fact]
    public async Task IntegerInput_RejectsText_AndKeepsTheModelValue()
    {
        using var _ = UiCulture.Use("en-US");
        var model = new TestProduct { Stock = 5 };
        var fieldErrors = new CardFieldErrors();
        var cut = RenderInput(model, Field(p => p.Stock), fieldErrors);

        await EnterTextAsync(cut, "abc");

        Assert.Equal(5, model.Stock);
        Assert.Equal("The Stock field must be a number.", fieldErrors.Get(nameof(TestProduct.Stock)));
        Assert.Equal("abc", cut.FindComponent<FluentTextInput>().Instance.Value);
    }

    [Fact]
    public async Task IntegerInput_RejectsFractions()
    {
        using var _ = UiCulture.Use("en-US");
        var model = new TestProduct { Stock = 5 };
        var fieldErrors = new CardFieldErrors();
        var cut = RenderInput(model, Field(p => p.Stock), fieldErrors);

        await EnterTextAsync(cut, "2.5");

        Assert.Equal(5, model.Stock);
        Assert.Equal("The Stock field must be a whole number.", fieldErrors.Get(nameof(TestProduct.Stock)));
    }

    [Fact]
    public async Task IntegerInput_AcceptsANumber_AndClearsThePendingError()
    {
        using var _ = UiCulture.Use("en-US");
        var model = new TestProduct { Stock = 5 };
        var fieldErrors = new CardFieldErrors();
        var cut = RenderInput(model, Field(p => p.Stock), fieldErrors);
        await EnterTextAsync(cut, "abc");

        await EnterTextAsync(cut, "7");

        Assert.Equal(7, model.Stock);
        Assert.Null(fieldErrors.Get(nameof(TestProduct.Stock)));
    }

    [Fact]
    public async Task DecimalInput_ParsesTheCommaOfAGermanUi()
    {
        using var _ = UiCulture.Use("de-DE");
        var model = new TestProduct { Price = 1m };
        var fieldErrors = new CardFieldErrors();
        var cut = RenderInput(model, Field(p => p.Price), fieldErrors);

        await EnterTextAsync(cut, "12,5");

        Assert.Equal(12.5m, model.Price);
        Assert.Null(fieldErrors.Get(nameof(TestProduct.Price)));
    }

    [Fact]
    public async Task DecimalInput_RejectsTheDotOfAGermanUi()
    {
        using var _ = UiCulture.Use("de-DE");
        var model = new TestProduct { Price = 1m };
        var fieldErrors = new CardFieldErrors();
        var cut = RenderInput(model, Field(p => p.Price), fieldErrors);

        await EnterTextAsync(cut, "12.5.1");

        Assert.Equal(1m, model.Price);
        Assert.NotNull(fieldErrors.Get(nameof(TestProduct.Price)));
    }

    [Fact]
    public void DecimalInput_UsesTheDecimalKeyboard()
    {
        var cut = RenderInput(new TestProduct(), Field(p => p.Price), new CardFieldErrors());

        Assert.Equal(TextInputMode.Decimal, cut.FindComponent<FluentTextInput>().Instance.InputMode);
    }

    [Fact]
    public void TemplateField_RendersItsLabelAndRequiredMark()
    {
        var field = Field(p => p.Name, f => f.EditorTemplate(_ => builder => builder.AddContent(0, "custom editor")));

        var cut = RenderInput(new TestProduct { Name = "N" }, field, new CardFieldErrors());

        Assert.Contains("Name", cut.Find("label.base-field-label").TextContent);
        Assert.NotNull(cut.Find("label.base-field-label .base-required-mark"));
        Assert.Contains("custom editor", cut.Markup);
    }

    [Fact]
    public void TemplateField_WithHiddenLabel_RendersNoLabel()
    {
        var field = Field(p => p.Name, f => f.HideLabel().EditorTemplate(_ => builder => builder.AddContent(0, "custom editor")));

        var cut = RenderInput(new TestProduct { Name = "N" }, field, new CardFieldErrors());

        Assert.Empty(cut.FindAll("label.base-field-label"));
    }

    [Fact]
    public void TemplateContext_CarriesTheLocalizedLabelAndRequiredFlag()
    {
        string? seenLabel = null;
        var seenRequired = false;
        var field = Field(p => p.Name, f => f.Label("Bezeichnung").EditorTemplate(context =>
        {
            seenLabel = context.Label;
            seenRequired = context.Required;
            return builder => builder.AddContent(0, "x");
        }));

        RenderInput(new TestProduct { Name = "N" }, field, new CardFieldErrors());

        Assert.Equal("Bezeichnung", seenLabel);
        Assert.True(seenRequired);
    }

    [Fact]
    public void EnumInput_ShowsTheLocalizedMemberText()
    {
        Services.AddSingleton<IStringLocalizerFactory>(new StubLocalizerFactory(typeof(ProductKind), new Dictionary<string, string>
        {
            [nameof(ProductKind.Digital)] = "Digitalprodukt"
        }));

        var cut = RenderInput(new TestProduct { Kind = ProductKind.Digital }, Field(p => p.Kind), new CardFieldErrors());

        var optionTexts = cut.FindComponent<FluentSelect<BasePropertyInput<TestProduct>.EnumOption, string>>().Instance.Items!.Select(option => option.Text).ToList();
        Assert.Contains(optionTexts, text => text.Contains("Digitalprodukt"));
        Assert.Contains(optionTexts, text => text.Contains("Physical"));
    }

    [Fact]
    public void ValidationMessage_IsRenderedNextToTheField()
    {
        var model = new TestProduct { Name = string.Empty };
        var editContext = new EditContext(model);
        var cut = RenderInput(model, Field(p => p.Name), new CardFieldErrors(), editContext);
        var store = new ValidationMessageStore(editContext);

        cut.InvokeAsync(() =>
        {
            store.Add(new FieldIdentifier(model, nameof(TestProduct.Name)), "Name is required");
            editContext.NotifyValidationStateChanged();
        });

        Assert.Equal("Name is required", cut.Find(".base-field-error").TextContent);
    }

    private static async Task EnterTextAsync(IRenderedComponent<BasePropertyInput<TestProduct>> cut, string text)
    {
        var textField = cut.FindComponent<FluentTextInput>();
        await cut.InvokeAsync(() => textField.Instance.ValueChanged.InvokeAsync(text));
    }

    private IRenderedComponent<BasePropertyInput<TestProduct>> RenderInput(
        TestProduct model,
        PropertyFieldConfig<TestProduct> field,
        CardFieldErrors fieldErrors,
        EditContext? editContext = null)
    {
        return Render<BasePropertyInput<TestProduct>>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.FieldConfig, field)
            .Add(p => p.IsEditing, true)
            .AddCascadingValue(editContext ?? new EditContext(model))
            .AddCascadingValue(fieldErrors));
    }

    private static PropertyFieldConfig<TestProduct> Field(
        Expression<Func<TestProduct, object?>> property,
        Action<FieldBuilder<TestProduct>>? configure = null)
    {
        return new BaseCardBuilder<TestProduct>().Field(property, configure).Build().Fields[0];
    }

    private sealed class StubLocalizerFactory(Type translatedType, Dictionary<string, string> translations) : IStringLocalizerFactory
    {
        public IStringLocalizer Create(Type resourceSource) =>
            new StubLocalizer(resourceSource == translatedType ? translations : []);

        public IStringLocalizer Create(string baseName, string location) => new StubLocalizer([]);
    }

    private sealed class StubLocalizer(Dictionary<string, string> translations) : IStringLocalizer
    {
        public LocalizedString this[string name] => translations.TryGetValue(name, out var value)
            ? new LocalizedString(name, value, resourceNotFound: false)
            : new LocalizedString(name, name, resourceNotFound: true);

        public LocalizedString this[string name, params object[] arguments] => this[name];

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            translations.Select(pair => new LocalizedString(pair.Key, pair.Value, resourceNotFound: false));
    }
}
