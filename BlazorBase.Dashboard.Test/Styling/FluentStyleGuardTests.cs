using BlazorBase.Components.Test.Infrastructure;
using Xunit;

namespace BlazorBase.Dashboard.Test.Styling;

/// <summary>
/// The dashboard and chart sources name only the BlazorBase semantic tokens and never reach into the internals of
/// Fluent's web components.
/// </summary>
public class FluentStyleGuardTests
{
    [Theory]
    [InlineData("BlazorBase.Dashboard")]
    [InlineData("BlazorBase.Chart")]
    public void ProjectSources_UseOnlySemanticTokensAndNoFluentInternals(string project)
    {
        var violations = FluentStyleGuard.FindViolations(FluentStyleGuard.ProjectDirectory(project));

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }
}
