using System.Text.Json.Serialization;

namespace BlazorBase.Chart.Models;

public class ChartTooltip
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Enabled { get; set; }
}
