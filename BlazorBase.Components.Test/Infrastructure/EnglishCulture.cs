using System.Globalization;

namespace BlazorBase.Components.Test.Infrastructure;

/// <summary>Switches the UI culture of the current test to English and restores it afterwards, for assertions on default texts.</summary>
public sealed class EnglishCulture : IDisposable
{
    private readonly CultureInfo Previous = CultureInfo.CurrentUICulture;

    public EnglishCulture() => CultureInfo.CurrentUICulture = new CultureInfo("en");

    public void Dispose() => CultureInfo.CurrentUICulture = Previous;
}
