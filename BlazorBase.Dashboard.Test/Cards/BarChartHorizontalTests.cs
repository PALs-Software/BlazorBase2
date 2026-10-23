using System.Text.Json;
using BlazorBase.Chart.Components;
using BlazorBase.Chart.Models;
using BlazorBase.Dashboard.Test.Infrastructure;
using Bunit;
using Xunit;

namespace BlazorBase.Dashboard.Test.Cards;

public sealed class BarChartHorizontalTests : DashboardTestContext
{
    private BunitJSModuleInterop ChartModule => JSInterop.SetupModule("./_content/BlazorBase.Chart/js/blazorChart.js");

    private ChartConfig RenderAndGetConfig(Action<ComponentParameterCollectionBuilder<BarChart>> parameters)
    {
        var module = ChartModule;
        module.Mode = JSRuntimeMode.Loose;

        var cut = Render<BarChart>(parameters);

        cut.WaitForAssertion(() => Assert.Single(module.Invocations["createChart"]));
        return Assert.IsType<ChartConfig>(module.Invocations["createChart"].Single().Arguments[1]);
    }

    [Fact]
    public void Horizontal_SetsTheIndexAxisToY()
    {
        var config = RenderAndGetConfig(p => p.Add(c => c.Horizontal, true).Add(c => c.Labels, ["A", "B"]));

        Assert.Equal("bar", config.Type);
        Assert.Equal("y", config.Options!.IndexAxis);
    }

    [Fact]
    public void Default_KeepsVerticalBars()
    {
        var config = RenderAndGetConfig(p => p.Add(c => c.Labels, ["A"]));

        Assert.Null(config.Options!.IndexAxis);
    }

    [Fact]
    public void Horizontal_KeepsTheScalesOfTheGivenOptions()
    {
        var options = new ChartOptions { Scales = new ChartScales { X = new ChartAxis { BeginAtZero = true } } };

        var config = RenderAndGetConfig(p => p.Add(c => c.Horizontal, true).Add(c => c.Options, options));

        Assert.True(config.Options!.Scales!.X!.BeginAtZero);
        Assert.Equal("y", config.Options.IndexAxis);
    }

    [Fact]
    public void IndexAxis_IsSerializedAsChartJsExpectsIt_AndOmittedWhenNotSet()
    {
        var horizontal = JsonSerializer.Serialize(new ChartOptions { IndexAxis = "y" }, JsonSerializerOptions.Web);
        var vertical = JsonSerializer.Serialize(new ChartOptions(), JsonSerializerOptions.Web);

        Assert.Contains("\"indexAxis\":\"y\"", horizontal);
        Assert.DoesNotContain("indexAxis", vertical);
    }
}
