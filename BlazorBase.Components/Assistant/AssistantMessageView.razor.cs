using BlazorBase.Components.Sanitization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace BlazorBase.Components.Assistant;

/// <summary>Renders one message of the conversation: the Markdown of an answer with code blocks, its sources, feedback and a pending action.</summary>
public sealed partial class AssistantMessageView(IServiceProvider serviceProvider, IStringLocalizer<BaseAssistantPanel> localizer) : ComponentBase
{
    #region Injects

    private readonly IServiceProvider ServiceProvider = serviceProvider;
    private readonly IStringLocalizer<BaseAssistantPanel> Localizer = localizer;

    #endregion

    /// <summary>The message to render.</summary>
    [Parameter, EditorRequired]
    public AssistantMessage Message { get; set; } = default!;

    /// <summary>The name of the assistant, read out before its answers by screen readers.</summary>
    [Parameter]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Raised when the user rates the answer.</summary>
    [Parameter]
    public EventCallback<AssistantRating> OnRate { get; set; }

    /// <summary>Raised when the user confirms the pending action.</summary>
    [Parameter]
    public EventCallback OnConfirm { get; set; }

    /// <summary>Raised when the user declines the pending action.</summary>
    [Parameter]
    public EventCallback OnDecline { get; set; }

    #region Members

    private string? RenderedText;
    private IReadOnlyList<AssistantMarkdownSegment> Segments = [];

    #endregion

    private bool CanRate => Message.Role == AssistantMessageRole.Assistant && !Message.IsStreaming && Message.Error is null && Message.Text.Length > 0;

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        if (Message.Role != AssistantMessageRole.Assistant || ReferenceEquals(RenderedText, Message.Text))
            return;

        RenderedText = Message.Text;
        var sanitizer = ServiceProvider.GetService<IHtmlSanitizer>();
        Segments = AssistantMarkdown.Split(RenderedText)
            .Select(segment => sanitizer is null || segment.IsCode ? segment : segment with { Html = sanitizer.Sanitize(segment.Html) })
            .ToArray();
    }

    private static string StateClass(AssistantPendingAction action) => action.State.ToString().ToLowerInvariant();

    private string StateText(AssistantActionState state) => state switch
    {
        AssistantActionState.Confirmed => Localizer["ActionConfirmed"],
        AssistantActionState.Declined => Localizer["ActionDeclined"],
        AssistantActionState.Failed => Localizer["ActionFailed"],
        _ => string.Empty
    };
}

internal static class AssistantUrl
{
    public static bool IsAbsoluteWebUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public static bool IsRelativeUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url) && url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal) && !url.Contains('\\');
}
