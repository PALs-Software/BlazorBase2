using BlazorBase.User.Services;
using BlazorBase.Components.Services;

namespace BlazorBase.User.Maui;

/// <summary>
/// The device form factor as reported by MAUI. A MAUI host talks to a server it does not
/// itself serve, so both idioms report a factor that keeps the server URL input visible in
/// <c>UserPreferencesPanel</c>.
/// </summary>
public class MauiFormFactor : IFormFactor
{
    public string GetFormFactor()
        => DeviceInfo.Current.Idiom == DeviceIdiom.Phone || DeviceInfo.Current.Idiom == DeviceIdiom.Tablet
            ? "Mobile"
            : "Desktop";

    public string GetPlatform() => DeviceInfo.Current.Platform.ToString();
}
