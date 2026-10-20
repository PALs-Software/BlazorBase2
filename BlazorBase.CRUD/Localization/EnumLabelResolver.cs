using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace BlazorBase.CRUD.Localization;

/// <summary>
/// Resolves the display text of an enum member for card editors and other read-only displays, so users never see the
/// raw CLR member name.
/// </summary>
/// <remarks>
/// Lookup order: the resource set of the enum type itself (key = member name, the convention the host enums already use),
/// then the model localizer chain with <c>&lt;EnumType&gt;_&lt;Member&gt;</c>, then <c>&lt;Property&gt;_&lt;Member&gt;</c>, and finally a
/// humanized form of the member name ("TypicalUser" becomes "Typical user").
/// </remarks>
public static partial class EnumLabelResolver
{
    /// <summary>Returns the localized display text for <paramref name="member"/>.</summary>
    /// <param name="services">Service provider used to create the enum type localizer.</param>
    /// <param name="modelLocalizer">The localizer chain of the owning model, or null.</param>
    /// <param name="propertyName">The name of the property holding the enum, or null.</param>
    /// <param name="enumType">The enum type.</param>
    /// <param name="member">The enum member.</param>
    public static string Resolve(IServiceProvider services, IStringLocalizer? modelLocalizer, string? propertyName, Type enumType, object member)
    {
        var name = member.ToString() ?? string.Empty;

        var enumLocalizer = services.GetService<IStringLocalizerFactory>()?.Create(enumType);
        if (enumLocalizer is not null && LocalizerResolver.ResolveOptional(enumLocalizer, name) is { } fromEnum)
            return fromEnum;

        if (modelLocalizer is not null)
        {
            if (LocalizerResolver.ResolveOptional(modelLocalizer, $"{enumType.Name}_{name}") is { } fromTypeKey)
                return fromTypeKey;

            if (propertyName is not null && LocalizerResolver.ResolveOptional(modelLocalizer, $"{propertyName}_{name}") is { } fromPropertyKey)
                return fromPropertyKey;
        }

        return Humanize(name);
    }

    /// <summary>Splits a PascalCase identifier into words: "TypicalUser" becomes "Typical user".</summary>
    public static string Humanize(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
            return identifier;

        var spaced = WordBoundary().Replace(identifier, " ").Trim();
        if (spaced.Length < 2)
            return spaced;

        return char.ToUpperInvariant(spaced[0]) + spaced[1..].ToLowerInvariant();
    }

    [GeneratedRegex(@"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])|_+")]
    private static partial Regex WordBoundary();
}
