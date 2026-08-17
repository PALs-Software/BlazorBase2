namespace BlazorBase.User.Server.Localization;

/// <summary>
/// Marker type for the server-side user messages that reach a client, resolved through
/// <c>IStringLocalizer&lt;BlazorBaseUserServerResources&gt;</c>. Developer-facing diagnostics —
/// startup failures, invariant violations — stay plain English and do not belong here.
/// </summary>
public sealed class BlazorBaseUserServerResources;
