namespace BlazorBase.Chart.Models;

public class ChartConfig
{
    public string Type { get; set; } = "bar";
    public ChartData Data { get; set; } = new();
    public ChartOptions? Options { get; set; }
}
