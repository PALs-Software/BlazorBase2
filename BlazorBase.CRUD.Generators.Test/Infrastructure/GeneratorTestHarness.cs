using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace BlazorBase.CRUD.Generators.Test.Infrastructure;

/// <summary>
/// Compiles a snippet of entity source against an in-memory definition of
/// <c>BlazorBase.CRUD.Attributes.BaseEntityAttribute</c>, runs <see cref="BaseDtoGenerator"/> over it,
/// and returns the generated sources plus diagnostics. The entity source only needs to declare a
/// namespace and the entity itself — the marker attribute and the implicit global usings that a real
/// SDK-style consumer project provides (including <c>System.Linq</c>, which the generated
/// <c>ProjectToDto</c> relies on) are supplied here.
/// </summary>
public static class GeneratorTestHarness
{
    private const string GlobalUsings = """
        global using System;
        global using System.Collections.Generic;
        global using System.Linq;
        global using BlazorBase.CRUD.Attributes;
        """;

    private const string AttributeSource = """
        using System;

        namespace BlazorBase.CRUD.Attributes
        {
            [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
            public sealed class BaseEntityAttribute : Attribute
            {
                public string? DtoName { get; set; }
                public string[]? ExcludeProperties { get; set; }
                public bool IncludeNavigationProperties { get; set; }
            }
        }
        """;

    private static readonly Type[] ForceLoadedRuntimeTypes =
        [typeof(System.Linq.Queryable), typeof(System.Linq.Enumerable)];

    public static GeneratorOutput Run(string entitySource)
    {
        _ = ForceLoadedRuntimeTypes;

        var trees = new[]
        {
            CSharpSyntaxTree.ParseText(GlobalUsings),
            CSharpSyntaxTree.ParseText(AttributeSource),
            CSharpSyntaxTree.ParseText(entitySource),
        };

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Select(assembly => (MetadataReference)MetadataReference.CreateFromFile(assembly.Location))
            .ToList();

        var compilation = CSharpCompilation.Create(
            "BlazorBaseGeneratorTests",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver
            .Create(new BaseDtoGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        var result = driver.GetRunResult().Results.Single();

        var outputErrors = outputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();

        return new GeneratorOutput(result.GeneratedSources, result.Diagnostics, result.Exception, outputErrors);
    }
}
