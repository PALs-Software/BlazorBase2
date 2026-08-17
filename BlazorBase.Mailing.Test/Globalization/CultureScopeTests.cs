using BlazorBase.Mailing.Globalization;
using System.Globalization;
using Xunit;

namespace BlazorBase.Mailing.Test.Globalization;

/// <summary>
/// Exercises <see cref="CultureScope"/>'s pin/restore/no-op semantics directly against
/// <see cref="CultureInfo.CurrentCulture"/> and <see cref="CultureInfo.CurrentUICulture"/>.
/// </summary>
public sealed class CultureScopeTests
{
    [Fact]
    public void Construct_WithCulture_PinsCurrentAndUICulture()
    {
        var germanCulture = CultureInfo.GetCultureInfo("de-DE");

        using var cultureScope = new CultureScope(germanCulture);

        Assert.Equal(germanCulture, CultureInfo.CurrentCulture);
        Assert.Equal(germanCulture, CultureInfo.CurrentUICulture);
    }

    [Fact]
    public void Dispose_RestoresCulture_CapturedBeforeConstruction()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUICulture = CultureInfo.CurrentUICulture;
        var germanCulture = CultureInfo.GetCultureInfo("de-DE");

        var cultureScope = new CultureScope(germanCulture);
        cultureScope.Dispose();

        Assert.Equal(previousCulture, CultureInfo.CurrentCulture);
        Assert.Equal(previousUICulture, CultureInfo.CurrentUICulture);
    }

    [Fact]
    public void Construct_WithNullCulture_LeavesAmbientCultureUnchanged()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUICulture = CultureInfo.CurrentUICulture;

        using var cultureScope = new CultureScope(null);

        Assert.Equal(previousCulture, CultureInfo.CurrentCulture);
        Assert.Equal(previousUICulture, CultureInfo.CurrentUICulture);
    }
}
