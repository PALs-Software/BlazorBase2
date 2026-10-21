namespace BlazorBase.Components.Layout;

/// <summary>
/// Handed to the sections a host adds to the <see cref="BaseSettingsPanel"/>, for what such a section may need
/// from the panel around it.
/// </summary>
/// <param name="CloseAsync">Closes the panel, e.g. before an action that leaves the current page.</param>
public sealed record SettingsPanelContext(Func<Task> CloseAsync);
