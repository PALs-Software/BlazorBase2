using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Localization;

namespace BlazorBase.CRUD.Validation;

/// <summary>
/// Builds the text of a failed DataAnnotations attribute in the UI language instead of the framework's built-in
/// English default.
/// </summary>
/// <remarks>
/// An attribute without an own message gets the framework resource that matches its type. An attribute with a literal
/// <see cref="ValidationAttribute.ErrorMessage"/> is looked up in the model localizer first (the key is the literal text,
/// as the host models already use it), so consumers can translate their own messages. Anything else keeps the
/// attribute's own result.
/// </remarks>
public static class DataAnnotationMessages
{
    /// <summary>Returns the message for a failed <paramref name="attribute"/>.</summary>
    /// <param name="attribute">The failed attribute.</param>
    /// <param name="label">The localized field label substituted for <c>{0}</c>.</param>
    /// <param name="attributeMessage">The message the attribute itself produced; used when nothing better is known.</param>
    /// <param name="frameworkLocalizer">The framework resource localizer holding the default templates.</param>
    /// <param name="modelLocalizer">The model localizer chain, or null.</param>
    public static string Build(
        ValidationAttribute attribute,
        string label,
        string attributeMessage,
        IStringLocalizer frameworkLocalizer,
        IStringLocalizer? modelLocalizer)
    {
        if (!string.IsNullOrEmpty(attribute.ErrorMessageResourceName))
            return attributeMessage;

        if (!string.IsNullOrEmpty(attribute.ErrorMessage))
            return FromModelLocalizer(attribute, label, attributeMessage, modelLocalizer);

        var key = ResolveKey(attribute);
        if (key is null)
            return attributeMessage;

        var template = frameworkLocalizer[key];
        if (template.ResourceNotFound)
            return attributeMessage;

        return Format(template.Value, attribute, label) ?? attributeMessage;
    }

    private static string FromModelLocalizer(ValidationAttribute attribute, string label, string attributeMessage, IStringLocalizer? modelLocalizer)
    {
        if (modelLocalizer is null)
            return attributeMessage;

        var template = modelLocalizer[attribute.ErrorMessage!];
        if (template.ResourceNotFound)
            return attributeMessage;

        return Format(template.Value, attribute, label) ?? attributeMessage;
    }

    private static string? ResolveKey(ValidationAttribute attribute) => attribute switch
    {
        RequiredAttribute => "Validation_Required",
        StringLengthAttribute { MinimumLength: > 0 } => "Validation_StringLengthRange",
        StringLengthAttribute => "Validation_StringLength",
        MinLengthAttribute => "Validation_MinLength",
        MaxLengthAttribute => "Validation_MaxLength",
        RangeAttribute => "Validation_Range",
        EmailAddressAttribute => "Validation_EmailAddress",
        PhoneAttribute => "Validation_Phone",
        UrlAttribute => "Validation_Url",
        RegularExpressionAttribute => "Validation_RegularExpression",
        _ => null
    };

    private static string? Format(string template, ValidationAttribute attribute, string label)
    {
        object?[] arguments = attribute switch
        {
            StringLengthAttribute stringLength => [label, stringLength.MaximumLength, stringLength.MinimumLength],
            MinLengthAttribute minLength => [label, minLength.Length],
            MaxLengthAttribute maxLength => [label, maxLength.Length],
            RangeAttribute range => [label, range.Minimum, range.Maximum],
            RegularExpressionAttribute regularExpression => [label, regularExpression.Pattern],
            _ => [label]
        };

        try
        {
            return string.Format(System.Globalization.CultureInfo.CurrentCulture, template, arguments);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
