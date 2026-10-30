namespace BlazorBase.Notifications.Server.Services;

/// <summary>
/// The content of one outgoing Web Push message. Deliberately small: only enough to render a native
/// OS notification and route a click. Anything more detailed is fetched by the app itself after the
/// user clicks through, so the encrypted payload never needs to carry sensitive data.
/// </summary>
/// <param name="Title">Notification title.</param>
/// <param name="Body">Short notification text.</param>
/// <param name="Url">Where the app should navigate to when the notification is clicked.</param>
/// <param name="Tag">
/// Optional Notifications API tag. Two pushes with the same tag replace each other on screen instead
/// of stacking.
/// </param>
public sealed record PushPayload(string Title, string Body, string? Url = null, string? Tag = null);
