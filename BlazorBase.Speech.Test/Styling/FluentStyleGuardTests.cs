using BlazorBase.Components.Test.Infrastructure;
using Xunit;

namespace BlazorBase.Speech.Test.Styling;

/// <summary>
/// The speech sources name only the BlazorBase semantic tokens and never reach into the internals of
/// Fluent's web components.
/// </summary>
public class FluentStyleGuardTests
{
    [Fact]
    public void SpeechSources_UseOnlySemanticTokensAndNoFluentInternals()
    {
        var violations = FluentStyleGuard.FindViolations(FluentStyleGuard.ProjectDirectory("BlazorBase.Speech"));

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }
}
