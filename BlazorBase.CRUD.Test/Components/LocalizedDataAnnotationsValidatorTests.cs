using BlazorBase.CRUD.Localization;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Validation;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using BlazorBase.CRUD.Validation;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

[Collection(CultureCollection.Name)]
public class LocalizedDataAnnotationsValidatorTests : BunitTestContextBase
{
    [Fact]
    public void Validate_ReportsTheLocalizedRequiredMessage_NextToTheLocalizedLabel()
    {
        using var _ = UiCulture.Use("de-DE");
        var model = new TestProduct { Name = string.Empty };
        var editContext = new EditContext(model);
        RenderValidator(editContext, new CardFieldErrors(), name => name == nameof(TestProduct.Name) ? "Bezeichnung" : name);

        var valid = editContext.Validate();

        Assert.False(valid);
        var message = Assert.Single(editContext.GetValidationMessages(new FieldIdentifier(model, nameof(TestProduct.Name))));
        Assert.Equal("Das Feld „Bezeichnung“ ist erforderlich.", message);
    }

    [Fact]
    public void FieldChanged_RevalidatesOnlyThatField()
    {
        using var _ = UiCulture.Use("en-US");
        var model = new TestProduct { Name = string.Empty };
        var editContext = new EditContext(model);
        RenderValidator(editContext, new CardFieldErrors(), name => name);
        editContext.Validate();

        model.Name = "Widget";
        editContext.NotifyFieldChanged(new FieldIdentifier(model, nameof(TestProduct.Name)));

        Assert.Empty(editContext.GetValidationMessages());
    }

    [Fact]
    public void PendingInputError_KeepsTheFormInvalid_UntilItIsCleared()
    {
        using var _ = UiCulture.Use("en-US");
        var model = new TestProduct { Name = "Widget" };
        var editContext = new EditContext(model);
        var fieldErrors = new CardFieldErrors();
        RenderValidator(editContext, fieldErrors, name => name);
        fieldErrors.Set(nameof(TestProduct.Stock), "The Stock field must be a number.");

        Assert.False(editContext.Validate());
        Assert.Equal("The Stock field must be a number.",
            Assert.Single(editContext.GetValidationMessages(new FieldIdentifier(model, nameof(TestProduct.Stock)))));

        fieldErrors.Clear(nameof(TestProduct.Stock));

        Assert.True(editContext.Validate());
    }

    private void RenderValidator(EditContext editContext, CardFieldErrors fieldErrors, Func<string, string> labelResolver)
    {
        var framework = TestFrameworkLocalizer.Create();

        Render<CascadingValue<EditContext>>(parameters => parameters
            .Add(p => p.Value, editContext)
            .AddChildContent<LocalizedDataAnnotationsValidator>(validator => validator
                .Add(v => v.FrameworkLocalizer, framework)
                .Add(v => v.LabelResolver, labelResolver)
                .Add(v => v.FieldErrors, fieldErrors)));
    }
}
