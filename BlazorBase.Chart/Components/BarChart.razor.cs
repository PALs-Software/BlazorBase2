using BlazorBase.Chart.Models;
using Microsoft.AspNetCore.Components;

namespace BlazorBase.Chart.Components;

public partial class BarChart
{
    [Parameter] public List<string> Labels { get; set; } = [];
    [Parameter] public List<ChartDataset> Datasets { get; set; } = [];
    [Parameter] public ChartOptions? Options { get; set; }
    [Parameter] public string Style { get; set; } = "width: 100%; height: 300px;";

    /// <summary>
    /// Draws horizontal bars: the categories run along the y axis and the values along the x axis. Sets
    /// <see cref="ChartOptions.IndexAxis"/> to <c>y</c> on the options in use.
    /// </summary>
    [Parameter] public bool Horizontal { get; set; }

    private ChartConfig Config
    {
        get
        {
            var options = Options ?? new ChartOptions { Responsive = true, MaintainAspectRatio = false };

            if (Horizontal)
                options.IndexAxis = "y";

            return new ChartConfig
            {
                Type = "bar",
                Data = new ChartData { Labels = Labels, Datasets = Datasets },
                Options = options,
            };
        }
    }
}
