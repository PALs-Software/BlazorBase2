using BlazorBase.Components.Test.Infrastructure;
using Xunit;

namespace BlazorBase.Notifications.Test.Styling;

/// <summary>
/// The notification sources name only the BlazorBase semantic tokens and never reach into the internals of
/// Fluent's web components.
/// </summary>
public class FluentStyleGuardTests
{
    [Fact]
    public void NotificationSources_UseOnlySemanticTokensAndNoFluentInternals()
    {
        var violations = FluentStyleGuard.FindViolations(FluentStyleGuard.ProjectDirectory("BlazorBase.Notifications"));

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }
}
