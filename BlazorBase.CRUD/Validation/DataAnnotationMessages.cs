using System.ComponentModel.DataAnnotations;
using System.Reflection;
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
    /// <param name="labelResolver">Resolves the localized label of another property, used by attributes that name one.</param>
    public static string Build(
        ValidationAttribute attribute,
        string label,
        string attributeMessage,
        IStringLocalizer frameworkLocalizer,
        IStringLocalizer? modelLocalizer,
        Func<string, string>? labelResolver = null)
    {
        if (!string.IsNullOrEmpty(attribute.ErrorMessageResourceName))
            return attributeMessage;

        if (HasLiteralMessage(attribute))
            return FromModelLocalizer(attribute, label, attributeMessage, modelLocalizer, labelResolver);

        var key = ResolveKey(attribute);
        if (key is null)
            return attributeMessage;

        var template = frameworkLocalizer[key];
        if (template.ResourceNotFound)
            return attributeMessage;

        return Format(template.Value, attribute, label, labelResolver) ?? attributeMessage;
    }

    /// <summary>
    /// Whether the consumer wrote the message. The attributes derived from <see cref="DataTypeAttribute"/> and
    /// <see cref="CompareAttribute"/> store their English default in <see cref="ValidationAttribute.ErrorMessage"/>
    /// themselves, so a non-empty value alone does not say that anyone chose it.
    /// </summary>
    private static bool HasLiteralMessage(ValidationAttribute attribute)
    {
        if (string.IsNullOrEmpty(attribute.ErrorMessage))
            return false;

        return !string.Equals(attribute.ErrorMessage, CreateDefaultInstance(attribute.GetType())?.ErrorMessage, StringComparison.Ordinal);
    }

    private static ValidationAttribute? CreateDefaultInstance(Type attributeType)
    {
        try
        {
            if (attributeType == typeof(CompareAttribute))
                return new CompareAttribute("-");

            return attributeType.GetConstructor(Type.EmptyTypes) is null ? null : (ValidationAttribute?)Activator.CreateInstance(attributeType);
        }
        catch (Exception ex) when (ex is MissingMethodException or TargetInvocationException or ArgumentException)
        {
            return null;
        }
    }

    private static string FromModelLocalizer(ValidationAttribute attribute, string label, string attributeMessage, IStringLocalizer? modelLocalizer, Func<string, string>? labelResolver)
    {
        if (modelLocalizer is null)
            return attributeMessage;

        var template = modelLocalizer[attribute.ErrorMessage!];
        if (template.ResourceNotFound)
            return attributeMessage;

        return Format(template.Value, attribute, label, labelResolver) ?? attributeMessage;
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
        CompareAttribute => "Validation_Compare",
        CreditCardAttribute => "Validation_CreditCard",
        FileExtensionsAttribute => "Validation_FileExtensions",
        LengthAttribute => "Validation_Length",
        Base64StringAttribute => "Validation_Base64String",
        AllowedValuesAttribute => "Validation_AllowedValues",
        DeniedValuesAttribute => "Validation_DeniedValues",
        _ => null
    };

    private static string? Format(string template, ValidationAttribute attribute, string label, Func<string, string>? labelResolver)
    {
        object?[] arguments = attribute switch
        {
            StringLengthAttribute stringLength => [label, stringLength.MaximumLength, stringLength.MinimumLength],
            MinLengthAttribute minLength => [label, minLength.Length],
            MaxLengthAttribute maxLength => [label, maxLength.Length],
            LengthAttribute length => [label, length.MinimumLength, length.MaximumLength],
            RangeAttribute range => [label, range.Minimum, range.Maximum],
            RegularExpressionAttribute regularExpression => [label, regularExpression.Pattern],
            CompareAttribute compare => [label, labelResolver?.Invoke(compare.OtherProperty) ?? compare.OtherPropertyDisplayName ?? compare.OtherProperty],
            FileExtensionsAttribute fileExtensions => [label, FormatExtensions(fileExtensions.Extensions)],
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

    private static string FormatExtensions(string extensions) =>
        string.Join(", ", extensions
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(extension => extension.StartsWith('.') ? extension : $".{extension}"));
}
