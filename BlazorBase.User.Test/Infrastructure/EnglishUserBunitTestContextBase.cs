using System.Globalization;

namespace BlazorBase.User.Test.Infrastructure;

/// <summary>
/// A <see cref="UserBunitTestContextBase"/> that renders in English whatever the machine's culture is, so
/// assertions on localized text hold on a German build agent too. Restores the previous culture on dispose.
/// </summary>
public abstract class EnglishUserBunitTestContextBase : UserBunitTestContextBase
{
    private readonly CultureInfo PreviousUiCulture = CultureInfo.CurrentUICulture;

    protected EnglishUserBunitTestContextBase() => CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");

    protected override void Dispose(bool disposing)
    {
        CultureInfo.CurrentUICulture = PreviousUiCulture;
        base.Dispose(disposing);
    }
}
