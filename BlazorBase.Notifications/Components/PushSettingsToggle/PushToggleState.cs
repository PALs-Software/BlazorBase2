namespace BlazorBase.Notifications.Components.PushSettingsToggle;

/// <summary>Display/interaction state of <see cref="PushSettingsToggle"/>.</summary>
public enum PushToggleState
{
    /// <summary>The browser does not support Web Push at all.</summary>
    Unsupported,

    /// <summary>The user previously denied notification permission for this origin.</summary>
    Denied,

    /// <summary>A Push API subscription exists in this browser and is registered with the host.</summary>
    Enabled,

    /// <summary>Supported and not denied, but no subscription exists in this browser yet.</summary>
    Disabled,
}
