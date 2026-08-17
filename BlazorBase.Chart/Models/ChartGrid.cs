using System.Text.Json.Serialization;

namespace BlazorBase.Chart.Models;

public class ChartGrid
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Display { get; set; }
}
