using System.Globalization;

namespace BlazorBase.Mailing.Globalization;

/// <summary>
/// Temporarily pins <see cref="CultureInfo.CurrentCulture"/> and <see cref="CultureInfo.CurrentUICulture"/>
/// to a given culture for the lifetime of the scope, restoring the previously active culture on
/// <see cref="Dispose"/>. Constructing the scope with a <c>null</c> culture is a no-op — the ambient
/// culture is left untouched and <see cref="Dispose"/> restores nothing.
/// </summary>
public sealed class CultureScope : IDisposable
{
    private readonly CultureInfo previousCulture;
    private readonly CultureInfo previousUICulture;
    private readonly bool isActive;

    /// <summary>
    /// Captures the current culture and, when <paramref name="culture"/> is not <c>null</c>, pins
    /// <see cref="CultureInfo.CurrentCulture"/> and <see cref="CultureInfo.CurrentUICulture"/> to it.
    /// </summary>
    /// <param name="culture">The culture to pin, or <c>null</c> to leave the ambient culture untouched.</param>
    public CultureScope(CultureInfo? culture)
    {
        previousCulture = CultureInfo.CurrentCulture;
        previousUICulture = CultureInfo.CurrentUICulture;
        isActive = culture is not null;

        if (!isActive)
            return;

        CultureInfo.CurrentCulture = culture!;
        CultureInfo.CurrentUICulture = culture!;
    }

    /// <summary>
    /// Restores the culture captured at construction time. A no-op when the scope was constructed
    /// with a <c>null</c> culture.
    /// </summary>
    public void Dispose()
    {
        if (!isActive)
            return;

        CultureInfo.CurrentCulture = previousCulture;
        CultureInfo.CurrentUICulture = previousUICulture;
    }
}
