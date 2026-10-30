using BlazorBase.Components.Test.Infrastructure;
using Xunit;

namespace BlazorBase.Components.Test.Styling;

/// <summary>
/// The product styles, scripts and markup of the shared components name only the BlazorBase semantic tokens and
/// never reach into the internals of Fluent's web components.
/// </summary>
public class FluentStyleGuardTests
{
    [Fact]
    public void BlazorBaseComponents_UseOnlySemanticTokensAndNoFluentInternals()
    {
        var violations = FluentStyleGuard.FindViolations(FluentStyleGuard.ProjectDirectory("BlazorBase.Components"));

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Theory]
    [InlineData("a.css", "p { color: var(--neutral-foreground-rest); }")]
    [InlineData("a.css", "p { color: var(--accent-fill-rest); }")]
    [InlineData("a.css", "p { font-size: var(--type-ramp-base-font-size); }")]
    [InlineData("a.css", "p { font-family: var(--body-font); }")]
    [InlineData("a.css", "p { gap: calc(var(--design-unit) * 1px); }")]
    [InlineData("a.css", "p { border-radius: var(--control-corner-radius); }")]
    [InlineData("a.css", "p { border-radius: var(--layer-corner-radius); }")]
    [InlineData("a.css", "p { height: calc(var(--base-height-multiplier) * 1px); }")]
    [InlineData("a.css", "p { border-width: var(--stroke-width); }")]
    [InlineData("a.css", "p { outline-color: var(--focus-stroke-outer); }")]
    [InlineData("a.css", "p { color: var(--error); }")]
    [InlineData("a.css", "p { color: var(--fill-color); }")]
    [InlineData("a.css", "p { color: var(--colorNeutralForeground1); }")]
    [InlineData("a.css", "p { padding: var(--spacingHorizontalM); }")]
    [InlineData("a.css", "fluent-button::part(control) { color: red; }")]
    [InlineData("a.css", "fluent-button { color: red; }")]
    [InlineData("a.css", ".x ::deep fluent-select { color: red; }")]
    [InlineData("a.css", ".fluent-nav-item { color: red; }")]
    [InlineData("a.js", "document.querySelector('fluent-dialog');")]
    [InlineData("a.js", "const t = 'var(--neutral-fill-rest)';")]
    [InlineData("a.razor", "<div style=\"color: var(--accent-fill-rest)\"></div>")]
    [InlineData("a.razor", "@using Microsoft.FluentUI.AspNetCore.Components.Migration")]
    [InlineData("a.cs", "var style = \"color: var(--neutral-foreground-rest)\";")]
    public void TheGuard_FlagsWhatItIsMeantToFlag(string fileName, string text)
    {
        Assert.NotEmpty(FluentStyleGuard.FindViolationsInText(fileName, text));
    }

    [Theory]
    [InlineData("a.css", "p { color: var(--color-accent); gap: var(--space-2); border-radius: var(--radius-sm); }")]
    [InlineData("a.css", "p { color: var(--color-accent-text); border-width: var(--stroke-width-thin); font-size: var(--font-size-base); }")]
    [InlineData("a.css", "p { box-shadow: var(--shadow-md); min-height: var(--control-height-touch); }")]
    [InlineData("a.css", "/* fluent-button::part(control) and var(--neutral-fill-rest) are named in a comment */ p { color: red; }")]
    [InlineData("a.css", "@import url(\"blazorbase-fluent-bridge.css\");")]
    [InlineData("a.js", "// fluent-dialog is only mentioned here\nconst x = 1;")]
    [InlineData("a.razor", "<FluentButton Appearance=\"ButtonAppearance.Primary\" Class=\"base-danger-action\" />")]
    [InlineData("a.razor", "@* fluent-button *@ <p>x</p>")]
    [InlineData("a.cs", "/// <summary>var(--neutral-fill-rest) in a doc comment</summary>\nvar x = 1;")]
    [InlineData("blazorbase-fluent-bridge.css", "body { --colorNeutralForeground1: var(--color-text-primary); }")]
    public void TheGuard_AllowsTheSemanticTokensCommentsAndTheBridge(string fileName, string text)
    {
        Assert.Empty(FluentStyleGuard.FindViolationsInText(fileName, text));
    }
}
