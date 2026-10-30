using BlazorBase.Components.Test.Infrastructure;
using Xunit;

namespace BlazorBase.User.Test.Styling;

public class FluentStyleGuardTests
{
    [Fact]
    public void UserSources_ContainNoV4TokensOrFluentSelectors()
    {
        var violations = FluentStyleGuard.FindViolations(FluentStyleGuard.ProjectDirectory("BlazorBase.User"));

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }
}
