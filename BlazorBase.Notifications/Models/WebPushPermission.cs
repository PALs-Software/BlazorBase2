namespace BlazorBase.Notifications.Models;

/// <summary>
/// The browser's current Web Push / Notifications API permission state for this origin, as
/// reported by <see cref="Interop.IWebPushClient.GetPermissionAsync"/>.
/// </summary>
public enum WebPushPermission
{
    /// <summary>The user has not yet been asked, or the browser does not support notifications.</summary>
    Default,

    /// <summary>The user allowed notifications for this origin.</summary>
    Granted,

    /// <summary>The user denied notifications for this origin; asking again has no effect.</summary>
    Denied,
}
