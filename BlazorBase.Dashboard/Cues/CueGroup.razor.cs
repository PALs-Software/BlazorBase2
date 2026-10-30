using Microsoft.AspNetCore.Components;

namespace BlazorBase.Dashboard;

/// <summary>
/// A row of <see cref="Cue"/> tiles. Tiles are 120 px (252 px when wide) and wrap on desktop; inside a role center narrower
/// than 640 px the group becomes two equal columns and wide tiles span both.
/// </summary>
public partial class CueGroup
{
    /// <summary>
    /// The cues.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Accessible name of the group, for example the title of the card around it. Optional.
    /// </summary>
    [Parameter]
    public string? Label { get; set; }
}
