using BlazorBase.Mailing.Globalization;
using System.Globalization;
using Xunit;

namespace BlazorBase.Mailing.Test.Globalization;

/// <summary>
/// Direct unit tests for <see cref="CultureScope"/>, run on the calling thread so that
/// the culture mutations are actually observable (unlike through <c>RazorEmailTemplateRenderer</c>,
/// where the dispatcher's captured execution context hides them). Each test pins a known
/// ambient culture first and restores the original in a <c>finally</c>, so a failing assertion
/// cannot leak a culture into whatever xUnit runs next on the same thread.
/// </summary>
public sealed class CultureScopeTests
{
    [Fact]
    public void Construct_WithCulture_PinsCurrentAndUICulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUICulture = CultureInfo.CurrentUICulture;
        try
        {
            var ambientCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentCulture = ambientCulture;
            CultureInfo.CurrentUICulture = ambientCulture;
            var targetCulture = CultureInfo.GetCultureInfo("de-DE");

            using var cultureScope = new CultureScope(targetCulture);

            Assert.Equal(targetCulture, CultureInfo.CurrentCulture);
            Assert.Equal(targetCulture, CultureInfo.CurrentUICulture);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUICulture;
        }
    }

    [Fact]
    public void Dispose_RestoresCulture_CapturedBeforeConstruction()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUICulture = CultureInfo.CurrentUICulture;
        try
        {
            var ambientCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentCulture = ambientCulture;
            CultureInfo.CurrentUICulture = ambientCulture;

            var cultureScope = new CultureScope(CultureInfo.GetCultureInfo("de-DE"));
            cultureScope.Dispose();

            Assert.Equal(ambientCulture, CultureInfo.CurrentCulture);
            Assert.Equal(ambientCulture, CultureInfo.CurrentUICulture);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUICulture;
        }
    }

    [Fact]
    public void Construct_WithNullCulture_LeavesAmbientCultureUnchanged()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUICulture = CultureInfo.CurrentUICulture;
        try
        {
            var ambientCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentCulture = ambientCulture;
            CultureInfo.CurrentUICulture = ambientCulture;

            using var cultureScope = new CultureScope(null);

            Assert.Equal(ambientCulture, CultureInfo.CurrentCulture);
            Assert.Equal(ambientCulture, CultureInfo.CurrentUICulture);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUICulture;
        }
    }

    [Fact]
    public void Dispose_AfterNullConstruction_DoesNotResetCultureChangedInsideScope()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUICulture = CultureInfo.CurrentUICulture;
        try
        {
            var ambientCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentCulture = ambientCulture;
            CultureInfo.CurrentUICulture = ambientCulture;

            var cultureScope = new CultureScope(null);
            var changedCulture = CultureInfo.GetCultureInfo("de-DE");
            CultureInfo.CurrentCulture = changedCulture;
            CultureInfo.CurrentUICulture = changedCulture;

            cultureScope.Dispose();

            Assert.Equal(changedCulture, CultureInfo.CurrentCulture);
            Assert.Equal(changedCulture, CultureInfo.CurrentUICulture);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUICulture;
        }
    }

    [Fact]
    public void NestedScopes_UnwindInReverseOrder()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUICulture = CultureInfo.CurrentUICulture;
        try
        {
            var startCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentCulture = startCulture;
            CultureInfo.CurrentUICulture = startCulture;

            var outerCulture = CultureInfo.GetCultureInfo("en-US");
            var innerCulture = CultureInfo.GetCultureInfo("de-DE");

            using (var outerScope = new CultureScope(outerCulture))
            {
                Assert.Equal(outerCulture, CultureInfo.CurrentCulture);
                Assert.Equal(outerCulture, CultureInfo.CurrentUICulture);

                using (var innerScope = new CultureScope(innerCulture))
                {
                    Assert.Equal(innerCulture, CultureInfo.CurrentCulture);
                    Assert.Equal(innerCulture, CultureInfo.CurrentUICulture);
                }

                Assert.Equal(outerCulture, CultureInfo.CurrentCulture);
                Assert.Equal(outerCulture, CultureInfo.CurrentUICulture);
            }

            Assert.Equal(startCulture, CultureInfo.CurrentCulture);
            Assert.Equal(startCulture, CultureInfo.CurrentUICulture);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUICulture;
        }
    }
}
