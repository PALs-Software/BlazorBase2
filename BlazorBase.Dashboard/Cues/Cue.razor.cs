using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using BlazorBase.Dashboard.Localization;

namespace BlazorBase.Dashboard;

/// <summary>
/// A cue tile: one number with a label, a traffic-light style and, as a link, the drill-down into the list it counts.
/// </summary>
public partial class Cue(IStringLocalizer<DashboardResources> localizer)
{
    #region Injects

    private readonly IStringLocalizer<DashboardResources> Localizer = localizer;

    #endregion

    #region Parameters

    /// <summary>
    /// The already formatted number or text, for example <c>3</c>, <c>72 %</c> or <c>$1,214</c>.
    /// </summary>
    [Parameter]
    public string Value { get; set; } = String.Empty;

    /// <summary>
    /// Small text right behind the value, for example <c>/62</c> or <c>min</c>.
    /// </summary>
    [Parameter]
    public string? Unit { get; set; }

    /// <summary>
    /// A line at the bottom of the tile, for example <c>Projection $1,840 · budget $2,000</c>. Best with <see cref="Wide"/>.
    /// </summary>
    [Parameter]
    public string? SubValue { get; set; }

    /// <summary>
    /// What the number counts, for example <c>Boosts to decide</c>. Required for the accessible name.
    /// </summary>
    [Parameter, EditorRequired]
    public string Label { get; set; } = String.Empty;

    /// <summary>
    /// The traffic-light style; shown as a stripe and a status icon and read out in words. Only <see cref="CueStyle.Unfavorable"/>
    /// also colours the number.
    /// </summary>
    [Parameter]
    public CueStyle Style { get; set; } = CueStyle.None;

    /// <summary>
    /// The drill-down target. With a value the tile is a link; without one it is a plain tile.
    /// </summary>
    [Parameter]
    public string? Href { get; set; }

    /// <summary>
    /// Doubles the width (252 px instead of 120 px); on phones it spans both columns.
    /// </summary>
    [Parameter]
    public bool Wide { get; set; }

    /// <summary>
    /// Optional trend line, drawn as a tiny sparkline next to the value (best with <see cref="Wide"/>); needs two or more points.
    /// </summary>
    [Parameter]
    public IReadOnlyList<double>? SparklinePoints { get; set; }

    /// <summary>
    /// Optional progress ring, 0 to 100 (values outside are clamped), next to the value (best with <see cref="Wide"/>).
    /// </summary>
    [Parameter]
    public double? RingPercent { get; set; }

    /// <summary>
    /// Shows a skeleton instead of the content while the number is being loaded.
    /// </summary>
    [Parameter]
    public bool Loading { get; set; }

    /// <summary>
    /// Extra CSS classes for the root element.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// Further attributes for the root element, such as a <c>data-testid</c>.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    #endregion

    private bool IsLink => !String.IsNullOrWhiteSpace(Href);

    private bool ShowRing => RingPercent is not null;

    private string RingDash
    {
        get
        {
            var percent = Math.Clamp(RingPercent ?? 0, 0, 100);
            return String.Create(CultureInfo.InvariantCulture, $"{percent:0.##} 100");
        }
    }

    private string? SparklineGeometry
    {
        get
        {
            if (SparklinePoints is not { Count: >= 2 } points)
                return null;

            var min = points.Min();
            var max = points.Max();
            var span = max - min;
            var builder = new StringBuilder();

            for (var index = 0; index < points.Count; index++)
            {
                var x = 90d * index / (points.Count - 1);
                var y = span <= 0 ? 17d : 30d - (points[index] - min) / span * 26d;
                builder.Append(x.ToString("0.##", CultureInfo.InvariantCulture)).Append(',').Append(y.ToString("0.##", CultureInfo.InvariantCulture)).Append(' ');
            }

            return builder.ToString().TrimEnd();
        }
    }

    private string RootClass
    {
        get
        {
            var style = Style.ToString().ToLowerInvariant();
            return $"cue cue--{style}{(Wide ? " cue--wide" : String.Empty)}{(IsLink ? " cue--link" : String.Empty)}{(Loading ? " cue--loading" : String.Empty)}{(String.IsNullOrWhiteSpace(Class) ? String.Empty : " " + Class)}";
        }
    }

    /// <summary>
    /// The accessible name: <c>&lt;value&gt; &lt;label&gt;, &lt;style in words&gt;</c>, followed by the sub value when there is one.
    /// </summary>
    internal string AriaText
    {
        get
        {
            if (Loading)
                return $"{Label}, {Localizer["CueLoading"].Value}";

            var value = String.IsNullOrEmpty(Unit) ? Value : (Char.IsLetter(Unit[0]) ? $"{Value} {Unit}" : $"{Value}{Unit}");
            var text = new StringBuilder($"{value} {Label}".Trim());
            var style = StyleWords;

            if (style is not null)
                text.Append(", ").Append(style);

            if (!String.IsNullOrWhiteSpace(SubValue))
                text.Append(", ").Append(SubValue);

            return text.ToString();
        }
    }

    private string? StyleWords => Style switch
    {
        CueStyle.Favorable => Localizer["CueStyleFavorable"].Value,
        CueStyle.Ambiguous => Localizer["CueStyleAmbiguous"].Value,
        CueStyle.Unfavorable => Localizer["CueStyleUnfavorable"].Value,
        CueStyle.Subordinate => Localizer["CueStyleSubordinate"].Value,
        _ => null,
    };
}
