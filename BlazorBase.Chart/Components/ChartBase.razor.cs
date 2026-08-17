using BlazorBase.Chart.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorBase.Chart.Components;

public partial class ChartBase : IAsyncDisposable
{
    [Parameter] public ChartConfig Config { get; set; } = new();
    [Parameter] public string Style { get; set; } = "width: 100%; height: 300px;";

    [Inject] private IJSRuntime Js { get; set; } = default!;

    private ChartInterop? Interop;
    private string CanvasId = $"chart-{Guid.NewGuid():N}";
    private bool IsRendered;

    protected override void OnInitialized()
    {
        Interop = new ChartInterop(Js);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && Interop is not null)
        {
            await Interop.CreateAsync(CanvasId, Config);
            IsRendered = true;
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (IsRendered && Interop is not null)
            await Interop.UpdateAsync(CanvasId, Config);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interop is null)
            return;

        if (IsRendered)
        {
            try { await Interop.DestroyAsync(CanvasId); }
            catch { }
        }
        await Interop.DisposeAsync();
    }
}
