using System.Text.Json.Serialization;

namespace BlazorBase.Chart.Models;

public class ChartPlugins
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ChartLegend? Legend { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ChartTitle? Title { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ChartTooltip? Tooltip { get; set; }
}
