using BlazorBase.Components.Test.Infrastructure;

namespace AppTemplate.Tests.Styling;

public class FluentStyleGuardTests
{
    [Fact]
    public void ClientSources_ContainNoV4TokensOrFluentSelectors()
    {
        var violations = FluentStyleGuard.FindViolations(FluentStyleGuard.ProjectDirectory("AppTemplate.Client"));

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }
}
