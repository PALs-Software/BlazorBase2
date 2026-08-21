using Microsoft.Extensions.Localization;

namespace BlazorBase.Localization;

/// <summary>
/// Walks through multiple localizers in priority order and returns the first hit where the key was actually found.
/// Falls back to the raw key when no localizer resolves it.
/// </summary>
public sealed class ChainedLocalizer(IReadOnlyList<IStringLocalizer> localizers) : IStringLocalizer
{
    #region Injects
    private readonly IReadOnlyList<IStringLocalizer> Localizers = localizers;
    #endregion

    public LocalizedString this[string name] => Resolve(name, null);

    public LocalizedString this[string name, params object[] arguments] => Resolve(name, arguments);

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        Localizers.SelectMany(l => l.GetAllStrings(includeParentCultures));

    private LocalizedString Resolve(string name, object[]? arguments)
    {
        foreach (var localizer in Localizers)
        {
            var result = arguments is null ? localizer[name] : localizer[name, arguments];
            if (!result.ResourceNotFound)
                return result;
        }

        return new LocalizedString(name, name, resourceNotFound: true);
    }
}
