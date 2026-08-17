using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace BlazorBase.CRUD.Generators.Test.Infrastructure;

/// <summary>
/// The result of running <see cref="BaseDtoGenerator"/> over a test compilation: the emitted
/// source files, any generator diagnostics or exception, and the errors of the compilation after
/// the generated sources were added back (to prove the generated code itself compiles).
/// </summary>
public sealed record GeneratorOutput(
    ImmutableArray<GeneratedSourceResult> GeneratedSources,
    ImmutableArray<Diagnostic> GeneratorDiagnostics,
    Exception? Exception,
    ImmutableArray<Diagnostic> OutputErrors)
{
    public bool Has(string hintName) => GeneratedSources.Any(source => source.HintName == hintName);

    public string SourceFor(string hintName)
        => GeneratedSources.Single(source => source.HintName == hintName).SourceText.ToString();
}
