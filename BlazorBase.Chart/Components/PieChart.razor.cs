using BlazorBase.Chart.Models;
using Microsoft.AspNetCore.Components;

namespace BlazorBase.Chart.Components;

public partial class PieChart
{
    [Parameter] public List<string> Labels { get; set; } = [];
    [Parameter] public List<ChartDataset> Datasets { get; set; } = [];
    [Parameter] public ChartOptions? Options { get; set; }
    [Parameter] public string Style { get; set; } = "width: 100%; height: 300px;";

    private ChartConfig Config => new()
    {
        Type = "pie",
        Data = new ChartData { Labels = Labels, Datasets = Datasets },
        Options = Options ?? new ChartOptions { Responsive = true, MaintainAspectRatio = false },
    };
}
