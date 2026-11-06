using System.Globalization;
using System.Resources;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Components.Services;

/// <summary>
/// Supplies the texts of the Fluent UI components in the current UI culture: English as a neutral resource, German as
/// a satellite. A key without a text answers itself.
/// </summary>
public sealed class BlazorBaseFluentLocalizer : IFluentLocalizer
{
    private static readonly ResourceManager Resources = new("BlazorBase.Components.Localization.FluentUiStrings", typeof(BlazorBaseFluentLocalizer).Assembly);

    /// <inheritdoc />
    public string this[string key, params object[] arguments]
    {
        get
        {
            var culture = CultureInfo.CurrentUICulture;
            var text = Resources.GetString(key, culture) ?? key;

            return arguments is { Length: > 0 } ? String.Format(culture, text, arguments) : text;
        }
    }
}
