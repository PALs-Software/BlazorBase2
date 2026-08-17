using BlazorBase.CRUD.Generators.Test.Infrastructure;
using Microsoft.CodeAnalysis;
using Xunit;

namespace BlazorBase.CRUD.Generators.Test;

/// <summary>
/// Tests for <see cref="BaseDtoGenerator"/> — the incremental source generator that emits a DTO and
/// <c>ToDto</c>/<c>ToEntity</c>/<c>ProjectToDto</c> mapping extensions for every <c>[BaseEntity]</c>
/// entity. Each test drives the generator through <see cref="GeneratorTestHarness"/> and asserts on the
/// emitted source.
/// </summary>
public sealed class BaseDtoGeneratorTests
{
    [Fact]
    public void GeneratesDto_WithDefaultName_AndMappingExtensions()
    {
        var output = GeneratorTestHarness.Run("""
            namespace Shop.Models;

            [BaseEntity]
            public class Product
            {
                public int Id { get; set; }
                public string Name { get; set; } = "";
            }
            """);

        AssertNoGeneratorErrors(output);
        Assert.True(output.Has("ProductDto.g.cs"));

        var source = output.SourceFor("ProductDto.g.cs");
        Assert.Contains("namespace Shop.Models;", source);
        Assert.Contains("public partial class ProductDto", source);
        Assert.Contains("public int Id { get; set; }", source);
        Assert.Contains("public string Name { get; set; }", source);
        Assert.Contains("public static class ProductMappingExtensions", source);
        Assert.Contains("public static ProductDto ToDto(this Product entity)", source);
        Assert.Contains("public static Product ToEntity(this ProductDto dto)", source);
        Assert.Contains("ProjectToDto(this System.Linq.IQueryable<Product> query)", source);

        Assert.Empty(output.OutputErrors);
    }

    [Fact]
    public void UsesCustomDtoName_WhenProvided()
    {
        var output = GeneratorTestHarness.Run("""
            namespace Shop.Models;

            [BaseEntity(DtoName = "ProductView")]
            public class Product
            {
                public int Id { get; set; }
            }
            """);

        AssertNoGeneratorErrors(output);
        Assert.True(output.Has("ProductView.g.cs"));

        var source = output.SourceFor("ProductView.g.cs");
        Assert.Contains("public partial class ProductView", source);
        Assert.Contains("public static ProductView ToDto(this Product entity)", source);
    }

    [Fact]
    public void ExcludesNamedProperties()
    {
        var output = GeneratorTestHarness.Run("""
            namespace Shop.Models;

            [BaseEntity(ExcludeProperties = new[] { "Secret" })]
            public class Product
            {
                public int Id { get; set; }
                public string Name { get; set; } = "";
                public string Secret { get; set; } = "";
            }
            """);

        AssertNoGeneratorErrors(output);

        var source = output.SourceFor("ProductDto.g.cs");
        Assert.Contains("public int Id", source);
        Assert.Contains("public string Name", source);
        Assert.DoesNotContain("Secret", source);
    }

    [Fact]
    public void ExcludesNavigationProperties_ByDefault()
    {
        var output = GeneratorTestHarness.Run("""
            namespace Shop.Models;

            public class Category { public int Id { get; set; } }
            public class Tag { public int Id { get; set; } }

            [BaseEntity]
            public class Product
            {
                public int Id { get; set; }
                public Category Category { get; set; } = new();
                public List<Tag> Tags { get; set; } = new();
            }
            """);

        AssertNoGeneratorErrors(output);

        var source = output.SourceFor("ProductDto.g.cs");
        Assert.Contains("public int Id", source);
        Assert.DoesNotContain("Category Category", source);
        Assert.DoesNotContain("Tags", source);
    }

    [Fact]
    public void IncludesNavigationProperties_WhenRequested()
    {
        var output = GeneratorTestHarness.Run("""
            namespace Shop.Models;

            public class Category { public int Id { get; set; } }
            public class Tag { public int Id { get; set; } }

            [BaseEntity(IncludeNavigationProperties = true)]
            public class Product
            {
                public int Id { get; set; }
                public Category Category { get; set; } = new();
                public List<Tag> Tags { get; set; } = new();
            }
            """);

        AssertNoGeneratorErrors(output);

        var source = output.SourceFor("ProductDto.g.cs");
        Assert.Contains("Category Category", source);
        Assert.Contains("Tags", source);
        Assert.Empty(output.OutputErrors);
    }

    [Fact]
    public void ReadOnlyProperty_HasNoSetter_AndIsOmittedFromMappings()
    {
        var output = GeneratorTestHarness.Run("""
            namespace Shop.Models;

            [BaseEntity]
            public class Product
            {
                public int Id { get; set; }
                public string DisplayName { get; } = "";
            }
            """);

        AssertNoGeneratorErrors(output);

        var source = output.SourceFor("ProductDto.g.cs");
        Assert.Contains("public string DisplayName { get; }", source);
        Assert.DoesNotContain("DisplayName =", source);
        Assert.Contains("Id = entity.Id", source);
    }

    [Fact]
    public void PreservesEntityNamespace()
    {
        var output = GeneratorTestHarness.Run("""
            namespace My.Custom.Models;

            [BaseEntity]
            public class Widget
            {
                public int Id { get; set; }
            }
            """);

        AssertNoGeneratorErrors(output);
        Assert.Contains("namespace My.Custom.Models;", output.SourceFor("WidgetDto.g.cs"));
    }

    [Fact]
    public void EmitsAutoGeneratedHeader_AndNullableEnable()
    {
        var output = GeneratorTestHarness.Run("""
            namespace Shop.Models;

            [BaseEntity]
            public class Product
            {
                public int Id { get; set; }
            }
            """);

        var source = output.SourceFor("ProductDto.g.cs");
        Assert.StartsWith("// <auto-generated />", source);
        Assert.Contains("#nullable enable", source);
    }

    [Fact]
    public void IgnoresStaticAndNonPublicProperties()
    {
        var output = GeneratorTestHarness.Run("""
            namespace Shop.Models;

            [BaseEntity]
            public class Product
            {
                public int Id { get; set; }
                public static int InstanceCount { get; set; }
                private string Hidden { get; set; } = "";
            }
            """);

        AssertNoGeneratorErrors(output);

        var source = output.SourceFor("ProductDto.g.cs");
        Assert.Contains("public int Id", source);
        Assert.DoesNotContain("InstanceCount", source);
        Assert.DoesNotContain("Hidden", source);
    }

    [Fact]
    public void ExcludeProperties_UnmatchedName_ProducesUnmatchedExcludeWarning_AndStillExcludesCorrectlyNamedProperty()
    {
        var output = GeneratorTestHarness.Run("""
            namespace Shop.Models;

            [BaseEntity(ExcludeProperties = new[] { "Secret", "Naem" })]
            public class Product
            {
                public int Id { get; set; }
                public string Name { get; set; } = "";
                public string Secret { get; set; } = "";
            }
            """);

        Assert.Null(output.Exception);

        var diagnostic = Assert.Single(output.GeneratorDiagnostics,
            candidate => candidate.Id == DiagnosticDescriptors.UnmatchedExcludePropertyId);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("Naem", diagnostic.GetMessage());
        Assert.Contains("Product", diagnostic.GetMessage());

        var source = output.SourceFor("ProductDto.g.cs");
        Assert.Contains("public int Id", source);
        Assert.Contains("public string Name", source);
        Assert.DoesNotContain("Secret", source);
    }

    [Fact]
    public void ExcludeProperties_AllNamesMatch_ProducesNoUnmatchedExcludeWarning()
    {
        var output = GeneratorTestHarness.Run("""
            namespace Shop.Models;

            [BaseEntity(ExcludeProperties = new[] { "Secret" })]
            public class Product
            {
                public int Id { get; set; }
                public string Name { get; set; } = "";
                public string Secret { get; set; } = "";
            }
            """);

        AssertNoGeneratorErrors(output);
        Assert.DoesNotContain(output.GeneratorDiagnostics,
            candidate => candidate.Id == DiagnosticDescriptors.UnmatchedExcludePropertyId);
    }

    [Fact]
    public void ExcludeProperties_NamesInheritedProperty_ProducesNoUnmatchedExcludeWarning()
    {
        var output = GeneratorTestHarness.Run("""
            namespace Shop.Models;

            public class AuditModel
            {
                public DateTime CreatedOn { get; set; }
            }

            [BaseEntity(ExcludeProperties = new[] { "CreatedOn" })]
            public class Product : AuditModel
            {
                public int Id { get; set; }
                public string Name { get; set; } = "";
            }
            """);

        AssertNoGeneratorErrors(output);
        Assert.DoesNotContain(output.GeneratorDiagnostics,
            candidate => candidate.Id == DiagnosticDescriptors.UnmatchedExcludePropertyId);

        var source = output.SourceFor("ProductDto.g.cs");
        Assert.Contains("public int Id", source);
        Assert.Contains("public string Name", source);
    }

    [Fact]
    public void ProducesNoOutput_WhenAttributeIsAbsent()
    {
        var output = GeneratorTestHarness.Run("""
            namespace Shop.Models;

            public class Product
            {
                public int Id { get; set; }
            }
            """);

        AssertNoGeneratorErrors(output);
        Assert.Empty(output.GeneratedSources);
    }

    private static void AssertNoGeneratorErrors(GeneratorOutput output)
    {
        Assert.Null(output.Exception);
        Assert.Empty(output.GeneratorDiagnostics);
    }
}
