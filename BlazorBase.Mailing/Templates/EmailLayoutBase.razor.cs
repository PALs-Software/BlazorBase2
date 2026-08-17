using Microsoft.AspNetCore.Components;

namespace BlazorBase.Mailing.Templates;

public partial class EmailLayoutBase
{
    [Parameter] public string Subject { get; set; } = string.Empty;
    [Parameter] public string PreheaderText { get; set; } = string.Empty;
    [Parameter] public string Language { get; set; } = "en";
    [Parameter] public string BrandName { get; set; } = "App";

    [Parameter] public string BackgroundColor { get; set; } = "#F4F5F7";
    [Parameter] public string ContainerColor { get; set; } = "#FFFFFF";
    [Parameter] public string HeaderColor { get; set; } = "#014B43";
    [Parameter] public string HeaderTextColor { get; set; } = "#FFFFFF";
    [Parameter] public string TextColor { get; set; } = "#1F2933";
    [Parameter] public string MutedTextColor { get; set; } = "#6B7280";
    [Parameter] public string BorderColor { get; set; } = "#E5E7EB";
    [Parameter] public string AccentColor { get; set; } = "#F6C445";
    [Parameter] public string AccentTextColor { get; set; } = "#1F2933";

    [Parameter] public string DisplayFontFamily { get; set; } = "'Plus Jakarta Sans'";
    [Parameter] public string BodyFontFamily { get; set; } = "'Noto Sans'";

    [Parameter] public RenderFragment? Header { get; set; }
    [Parameter] public RenderFragment? Body { get; set; }
    [Parameter] public RenderFragment? Footer { get; set; }
}
