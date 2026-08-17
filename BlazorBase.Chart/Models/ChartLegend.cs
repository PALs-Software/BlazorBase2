using System.Text.Json.Serialization;

namespace BlazorBase.Chart.Models;

public class ChartLegend
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Display { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Position { get; set; }
}
