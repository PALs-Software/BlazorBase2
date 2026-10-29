using BlazorBase.Components.Test.Infrastructure;
using Xunit;

namespace BlazorBase.Files.Test.Styling;

public class FluentStyleGuardTests
{
    [Fact]
    public void FileSources_ContainNoV4TokensOrFluentSelectors()
    {
        var violations = FluentStyleGuard.FindViolations(FluentStyleGuard.ProjectDirectory("BlazorBase.Files"));

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }
}
