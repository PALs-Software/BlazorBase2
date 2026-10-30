using BlazorBase.Components.Test.Infrastructure;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// The styles, scripts and markup of the CRUD components name only the BlazorBase semantic tokens and never reach into
/// the internals of Fluent's web components.
/// </summary>
public class FluentStyleGuardTests
{
    [Fact]
    public void BlazorBaseCrud_UsesOnlySemanticTokensAndNoFluentInternals()
    {
        var violations = FluentStyleGuard.FindViolations(FluentStyleGuard.ProjectDirectory("BlazorBase.CRUD"));

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }
}
