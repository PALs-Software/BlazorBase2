using System.Text.Json.Serialization;

namespace BlazorBase.Chart.Models;

public class ChartAxisTitle
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Display { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }
}
