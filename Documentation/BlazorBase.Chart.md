# BlazorBase.Chart

A thin Razor Class Library that wraps **Chart.js 4.5.1** behind strongly-typed Blazor components and a JS-interop bridge.

> Target framework: `net10.0` · No host-specific dependencies · Works in Server, WebAssembly and MAUI.

---

## Table of Contents

- [Architecture & Overview](#architecture--overview)
- [Getting Started](#getting-started)
- [Components](#components)
- [Models](#models)
- [Code Examples](#code-examples)
- [JS Interop Details](#js-interop-details)

---

## Architecture & Overview

```
┌────────────────────────────────────────────────────────┐
│  Components                                            │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐              │
│  │BarChart  │  │LineChart │  │PieChart  │              │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘              │
│       └────────────┬┴─────────────┘                    │
│                    ▼                                   │
│              ┌──────────┐                              │
│              │ChartBase │  ← renders <canvas>          │
│              └────┬─────┘                              │
├────────────────────┼───────────────────────────────────┤
│                    ▼                                   │
│         ┌──────────────────────┐                       │
│         │ ChartInterop         │  IJSObjectReference   │
│         │  CreateAsync         │                       │
│         │  UpdateAsync         │                       │
│         │  DestroyAsync        │                       │
│         └────────┬─────────────┘                       │
├──────────────────┼─────────────────────────────────────┤
│                  ▼                                     │
│  wwwroot/js/blazorChart.js   (ES module)               │
│    createChart / updateChart / destroyChart            │
│                                                        │
│  wwwroot/js/chart.umd.min.js  (Chart.js 4.5.1)         │
└────────────────────────────────────────────────────────┘
```

**Key ideas**
- All chart types compose `ChartBase`, which owns the `<canvas>` element and the lifecycle (`OnAfterRenderAsync` → create, `OnParametersSetAsync` → update, `DisposeAsync` → destroy).
- A single `ChartConfig` POCO is serialized as JSON and passed to Chart.js — every Chart.js option you'd write in JavaScript is just a property on `ChartConfig.Options`.
- The JS module is **lazy-loaded** via `import("./_content/BlazorBase.Chart/js/blazorChart.js")`. Chart.js itself (`chart.umd.min.js`) must be loaded by the host (see Getting Started).

---

## Getting Started

### 1. Add the project reference

```xml
<ProjectReference Include="..\Libs\BlazorBase.Chart\BlazorBase.Chart.csproj" />
```

### 2. Reference `chart.umd.min.js` in the host

`BlazorBase.Chart` ships `chart.umd.min.js` as a static web asset. Reference it from your host's HTML host file:

**`App.razor` (Server) / `index.html` (WASM / MAUI):**
```html
<script src="_content/BlazorBase.Chart/js/chart.umd.min.js"></script>
```

The internal ES module (`blazorChart.js`) is imported on demand — no manual loading needed.

### 3. Add the imports

`_Imports.razor` (or per-page `@using`) of the consuming project:
```razor
@using BlazorBase.Chart.Components
@using BlazorBase.Chart.Models
```

### 4. No DI registration required

The chart components instantiate `ChartInterop` themselves via injected `IJSRuntime`. There is no `AddBlazorBaseChart()` extension — just reference and use.

---

## Components

| Component | Description |
|---|---|
| `<BarChart>` | Vertical bar chart (`type: "bar"`) |
| `<LineChart>` | Line chart (`type: "line"`) |
| `<PieChart>` | Pie chart (`type: "pie"`) |
| `<ChartBase>` | Generic component for any Chart.js type via `ChartConfig` |

### Common Parameters (BarChart / LineChart / PieChart)

| Parameter | Type | Default | Description |
|---|---|---|---|
| `Labels` | `List<string>` | `[]` | Axis labels |
| `Datasets` | `List<ChartDataset>` | `[]` | One or more data series |
| `Options` | `ChartOptions?` | `null` (→ Responsive=true, MaintainAspectRatio=false) | Chart.js options |
| `Style` | `string` | `"width: 100%; height: 300px;"` | Inline CSS on the `<canvas>` |

### ChartBase Parameters

| Parameter | Type | Description |
|---|---|---|
| `Config` | `ChartConfig` | Full Chart.js config object |
| `Style` | `string` | Inline style for the canvas |

### Lifecycle

- **First render:** `Interop.CreateAsync(canvasId, Config)` — creates the Chart.js instance.
- **Parameter change:** `Interop.UpdateAsync(canvasId, Config)` — updates `chart.data` / `chart.options` and calls `chart.update()`.
- **Dispose:** `Interop.DestroyAsync(canvasId)` — destroys the chart and disposes the JS module.

---

## Models

All models live in the `BlazorBase.Chart.Models` namespace. They are POCOs decorated with `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` so unset properties are omitted from the JSON sent to Chart.js — keeping Chart.js defaults intact.

### `ChartConfig`
| Property | Type | Description |
|---|---|---|
| `Type` | `string` | Chart.js chart type (`"bar"`, `"line"`, `"pie"`, …) |
| `Data` | `ChartData` | Labels + datasets |
| `Options` | `ChartOptions?` | Display, scales, plugins |

### `ChartData`
| Property | Type |
|---|---|
| `Labels` | `List<string>` |
| `Datasets` | `List<ChartDataset>` |

### `ChartDataset`
| Property | Type | Notes |
|---|---|---|
| `Label` | `string?` | Series name |
| `Data` | `List<double>` | Values |
| `BackgroundColor` | `List<string>?` | Per-bar/slice colors |
| `BorderColor` | `List<string>?` | |
| `BorderWidth` | `double?` | |
| `BarPercentage` | `double?` | Bar width ratio |
| `CategoryPercentage` | `double?` | |
| `Fill` | `bool?` | Line fill |
| `Tension` | `double?` | Line smoothness |
| `PointRadius` | `double?` | |

### `ChartOptions`
| Property | Type | Default |
|---|---|---|
| `Responsive` | `bool?` | `true` |
| `MaintainAspectRatio` | `bool?` | `false` |
| `Plugins` | `ChartPlugins?` | — |
| `Scales` | `ChartScales?` | — |

### `ChartScales` / `ChartAxis` / `ChartAxisTicks` / `ChartAxisTitle` / `ChartGrid`
Mirror the Chart.js scales API. `ChartAxis` supports `Display`, `BeginAtZero`, `Stacked`, `Title`, `Ticks`, `Grid`.

### `ChartPlugins` / `ChartTitle` / `ChartLegend` / `ChartTooltip`
Mirror Chart.js plugin options. `ChartLegend.Position` accepts `"top"`, `"right"`, `"bottom"`, `"left"`.

---

## Code Examples

### Bar chart — simple

```razor
<BarChart Labels="@(["Mon", "Tue", "Wed", "Thu", "Fri"])"
          Datasets="@([
              new ChartDataset
              {
                  Label = "Sales",
                  Data = [12, 19, 3, 5, 2],
                  BackgroundColor = ["#3b82f6"]
              }])" />
```

### Line chart — multi-series with options

```csharp
private List<string> Labels { get; } = ["Q1", "Q2", "Q3", "Q4"];

private List<ChartDataset> Datasets { get; } =
[
    new()
    {
        Label = "2025",
        Data = [10, 25, 18, 30],
        BorderColor = ["#3b82f6"],
        Tension = 0.4,
        Fill = false,
    },
    new()
    {
        Label = "2026",
        Data = [15, 22, 28, 35],
        BorderColor = ["#10b981"],
        Tension = 0.4,
        Fill = false,
    },
];

private ChartOptions LineOptions { get; } = new()
{
    Responsive = true,
    MaintainAspectRatio = false,
    Plugins = new ChartPlugins
    {
        Legend = new ChartLegend { Display = true, Position = "top" },
        Title  = new ChartTitle  { Display = true, Text = "Yearly revenue" },
    },
    Scales = new ChartScales
    {
        Y = new ChartAxis { BeginAtZero = true },
    },
};
```

```razor
<LineChart Labels="Labels" Datasets="Datasets" Options="LineOptions" Style="width:100%; height:400px;" />
```

### Pie chart

```razor
<PieChart Labels="@(["Work", "Break", "Meeting", "Exercise"])"
          Datasets="@([
              new ChartDataset
              {
                  Data = [42, 12, 18, 8],
                  BackgroundColor = ["#3b82f6", "#10b981", "#f59e0b", "#ef4444"]
              }])" />
```

### Custom chart type via `ChartBase`

Use `ChartBase` directly when you need a Chart.js type that doesn't have a dedicated component (e.g. doughnut, radar, polarArea).

```csharp
private ChartConfig DoughnutConfig { get; } = new()
{
    Type = "doughnut",
    Data = new ChartData
    {
        Labels = ["Red", "Blue", "Yellow"],
        Datasets =
        [
            new ChartDataset
            {
                Data = [300, 50, 100],
                BackgroundColor = ["#ef4444", "#3b82f6", "#eab308"]
            }
        ],
    },
};
```

```razor
<ChartBase Config="DoughnutConfig" />
```

### Reactive updates

`ChartBase` calls `UpdateAsync` whenever its parameters change. Just reassign `Datasets`/`Labels` and call `StateHasChanged()` (or rely on Blazor's binding):

```csharp
private async Task RefreshAsync()
{
    var data = await StatsApi.GetAsync();
    Labels = data.Buckets;
    Datasets = [new ChartDataset { Label = "Hits", Data = data.Values }];
}
```

---

## JS Interop Details

`wwwroot/js/blazorChart.js` is a tiny module that owns a `charts[]` map:

```js
export function createChart(elementId, config) {
    destroyChart(elementId);
    const ctx = document.getElementById(elementId);
    if (!ctx) return;
    charts[elementId] = new Chart(ctx, config);
}
export function updateChart(elementId, config) { /* sets data/options, calls chart.update() */ }
export function destroyChart(elementId) { /* chart.destroy() */ }
```

The C# wrapper:

```csharp
public class ChartInterop : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> ModuleTask;

    public ChartInterop(IJSRuntime jsRuntime)
        => ModuleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/BlazorBase.Chart/js/blazorChart.js").AsTask());

    public async ValueTask CreateAsync(string elementId, object config)  { /* ... */ }
    public async ValueTask UpdateAsync(string elementId, object config)  { /* ... */ }
    public async ValueTask DestroyAsync(string elementId)                { /* ... */ }
}
```

Because the module is lazy-loaded, no JS is fetched until the first chart appears on screen.
