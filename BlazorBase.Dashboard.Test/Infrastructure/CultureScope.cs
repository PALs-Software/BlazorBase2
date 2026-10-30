using System.Globalization;

namespace BlazorBase.Dashboard.Test.Infrastructure;

/// <summary>
/// Switches the UI culture of the current test and restores it afterwards.
/// </summary>
public sealed class CultureScope : IDisposable
{
    private readonly CultureInfo PreviousUi = CultureInfo.CurrentUICulture;
    private readonly CultureInfo Previous = CultureInfo.CurrentCulture;

    public CultureScope(string name)
    {
        CultureInfo.CurrentUICulture = new CultureInfo(name);
        CultureInfo.CurrentCulture = new CultureInfo(name);
    }

    public void Dispose()
    {
        CultureInfo.CurrentUICulture = PreviousUi;
        CultureInfo.CurrentCulture = Previous;
    }
}
