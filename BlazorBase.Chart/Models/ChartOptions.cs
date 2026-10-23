using System.Text.Json.Serialization;

namespace BlazorBase.Chart.Models;

public class ChartOptions
{
    /// <summary>
    /// The axis that carries the categories of a bar chart: <c>x</c> (default, vertical bars) or <c>y</c> (horizontal bars).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IndexAxis { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Responsive { get; set; } = true;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? MaintainAspectRatio { get; set; } = false;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ChartPlugins? Plugins { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ChartScales? Scales { get; set; }
}
