using System.Text.Json.Serialization;

namespace BlazorBase.Chart.Models;

public class ChartAxisTicks
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Callback { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxTicksLimit { get; set; }
}
