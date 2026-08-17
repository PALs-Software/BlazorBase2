using Microsoft.Extensions.Localization;

namespace BlazorBase.CRUD.Localization;

/// <summary>
/// Fallback localizer that always reports ResourceNotFound, returning the raw key as value.
/// Used when neither a param localizer nor a model localizer is available.
/// </summary>
public sealed class EmptyLocalizer : IStringLocalizer
{
    public static readonly EmptyLocalizer Instance = new();

    private EmptyLocalizer() { }

    public LocalizedString this[string name] =>
        new(name, name, resourceNotFound: true);

    public LocalizedString this[string name, params object[] arguments] =>
        new(name, string.Format(name, arguments), resourceNotFound: true);

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        Enumerable.Empty<LocalizedString>();
}
