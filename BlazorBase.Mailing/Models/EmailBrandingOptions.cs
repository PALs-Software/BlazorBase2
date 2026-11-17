namespace BlazorBase.Mailing.Models;

/// <summary>
/// The brand the standard account emails (invitation, password reset, email confirmation) are rendered
/// with, bound from the <see cref="SectionName"/> configuration section by <c>AddBlazorBaseMailing</c>.
/// Every color is a CSS color value placed into the email's style block; the defaults match the
/// defaults of <see cref="Templates.EmailLayoutBase"/>.
/// </summary>
public class EmailBrandingOptions
{
    public const string SectionName = "EmailBranding";

    /// <summary>The product name shown in the header, the footer and the subject lines.</summary>
    public string BrandName { get; set; } = "App";

    public string HeaderColor { get; set; } = "#014B43";
    public string HeaderTextColor { get; set; } = "#FFFFFF";
    public string AccentColor { get; set; } = "#F6C445";
    public string AccentTextColor { get; set; } = "#1F2933";
}
