using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;

namespace BlazorBase.CRUD.Benchmarks;

public static class Program
{
    public static int Main(string[] args)
    {
        var config = ManualConfig.CreateEmpty()
            .AddLogger(ConsoleLogger.Default)
            .AddColumnProvider(BenchmarkDotNet.Columns.DefaultColumnProviders.Instance)
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddExporter(MarkdownExporter.GitHub)
            .AddExporter(JsonExporter.FullCompressed)
            .AddExporter(CsvExporter.Default)
            .WithOption(ConfigOptions.DisableOptimizationsValidator, false);

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
        return 0;
    }
}
