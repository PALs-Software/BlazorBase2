using System.Text.Json.Serialization;

namespace BlazorBase.Chart.Models;

public class ChartScales
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ChartAxis? X { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ChartAxis? Y { get; set; }
}
