using System.Globalization;

namespace BlazorBase.Speech.Test.Infrastructure;

/// <summary>Switches the UI culture for the lifetime of the scope and restores it afterwards.</summary>
public sealed class CultureScope : IDisposable
{
    private readonly CultureInfo PreviousCulture = CultureInfo.CurrentUICulture;

    public CultureScope(string cultureName)
        => CultureInfo.CurrentUICulture = new CultureInfo(cultureName);

    public void Dispose() => CultureInfo.CurrentUICulture = PreviousCulture;
}
