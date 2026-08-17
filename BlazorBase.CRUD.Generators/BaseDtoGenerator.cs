using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace BlazorBase.CRUD.Generators;

[Generator]
public class BaseDtoGenerator : IIncrementalGenerator
{
    private const string BaseEntityAttributeName = "BlazorBase.CRUD.Attributes.BaseEntityAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                BaseEntityAttributeName,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => GetEntityInfo(ctx))
            .Where(static info => info is not null);

        context.RegisterSourceOutput(classDeclarations, static (sourceContext, entityInfo) =>
        {
            if (entityInfo is null)
                return;

            var diagnosticLocation = entityInfo.UnmatchedExcludeLocation?.ToLocation() ?? Location.None;

            foreach (var unmatchedExcludeName in entityInfo.UnmatchedExcludeNames)
            {
                sourceContext.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.UnmatchedExcludeProperty,
                    diagnosticLocation,
                    unmatchedExcludeName,
                    entityInfo.EntityName));
            }

            var source = GenerateDtoAndMapping(entityInfo);
            sourceContext.AddSource($"{entityInfo.DtoName}.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }

    private static EntityInfo? GetEntityInfo(GeneratorAttributeSyntaxContext context)
    {
        var symbol = context.TargetSymbol as INamedTypeSymbol;

        if (symbol is null)
            return null;

        var attribute = symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == BaseEntityAttributeName);

        if (attribute is null)
            return null;

        string? customDtoName = null;
        string[]? excludeProperties = null;
        var includeNavigationProperties = false;

        foreach (var namedArg in attribute.NamedArguments)
        {
            switch (namedArg.Key)
            {
                case "DtoName":
                    customDtoName = namedArg.Value.Value as string;
                    break;
                case "ExcludeProperties":
                    excludeProperties = namedArg.Value.Values.Select(v => v.Value as string).Where(v => v is not null).ToArray()!;
                    break;
                case "IncludeNavigationProperties":
                    includeNavigationProperties = (bool)(namedArg.Value.Value ?? false);
                    break;
            }
        }

        var dtoName = customDtoName ?? $"{symbol.Name}Dto";
        var namespaceName = symbol.ContainingNamespace.ToDisplayString();
        var excludeNames = excludeProperties ?? new string[0];
        var excludeSet = new HashSet<string>(excludeNames);

        var actualPropertyNames = GetActualPropertyNames(symbol);

        var unmatchedExcludeNames = excludeNames
            .Where(name => !actualPropertyNames.Contains(name))
            .ToImmutableArray();

        LocationInfo? unmatchedExcludeLocation = null;

        if (unmatchedExcludeNames.Length > 0)
        {
            var attributeLocation = attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? context.TargetNode.GetLocation();
            unmatchedExcludeLocation = LocationInfo.CreateFrom(attributeLocation);
        }

        var properties = symbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic)
            .Where(p => !excludeSet.Contains(p.Name))
            .Where(p => includeNavigationProperties || !IsNavigationProperty(p))
            .Select(p => new PropertyInfo
            {
                Name = p.Name,
                TypeName = p.Type.ToDisplayString(),
                IsReadOnly = p.SetMethod is null
            })
            .ToImmutableArray();

        return new EntityInfo
        {
            EntityName = symbol.Name,
            DtoName = dtoName,
            Namespace = namespaceName,
            Properties = properties,
            UnmatchedExcludeNames = unmatchedExcludeNames,
            UnmatchedExcludeLocation = unmatchedExcludeLocation
        };
    }

    private static HashSet<string> GetActualPropertyNames(INamedTypeSymbol symbol)
    {
        var propertyNames = new HashSet<string>();
        var currentType = symbol;

        while (currentType is not null && currentType.SpecialType != SpecialType.System_Object)
        {
            var declaredPropertyNames = currentType.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(property => property.DeclaredAccessibility == Accessibility.Public && !property.IsStatic)
                .Select(property => property.Name);

            propertyNames.UnionWith(declaredPropertyNames);
            currentType = currentType.BaseType;
        }

        return propertyNames;
    }

    private static bool IsNavigationProperty(IPropertySymbol property)
    {
        var type = property.Type;

        if (type.TypeKind == TypeKind.Class && type.SpecialType == SpecialType.None
            && type.ToDisplayString() != "string")
            return true;

        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var genericDef = namedType.OriginalDefinition.ToDisplayString();
            if (genericDef.StartsWith("System.Collections.Generic.ICollection")
                || genericDef.StartsWith("System.Collections.Generic.IList")
                || genericDef.StartsWith("System.Collections.Generic.List"))
                return true;
        }

        return false;
    }

    private static string GenerateDtoAndMapping(EntityInfo info)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"namespace {info.Namespace};");
        sb.AppendLine();

        sb.AppendLine($"public partial class {info.DtoName}");
        sb.AppendLine("{");

        foreach (var prop in info.Properties)
        {
            var setter = prop.IsReadOnly ? "" : " set;";
            sb.AppendLine($"    public {prop.TypeName} {prop.Name} {{ get;{setter} }}");
        }

        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine($"public static class {info.EntityName}MappingExtensions");
        sb.AppendLine("{");
        sb.AppendLine($"    public static {info.DtoName} ToDto(this {info.EntityName} entity)");
        sb.AppendLine("    {");
        sb.AppendLine($"        return new {info.DtoName}");
        sb.AppendLine("        {");

        foreach (var prop in info.Properties.Where(p => !p.IsReadOnly))
        {
            sb.AppendLine($"            {prop.Name} = entity.{prop.Name},");
        }

        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    public static {info.EntityName} ToEntity(this {info.DtoName} dto)");
        sb.AppendLine("    {");
        sb.AppendLine($"        return new {info.EntityName}");
        sb.AppendLine("        {");

        foreach (var prop in info.Properties.Where(p => !p.IsReadOnly))
        {
            sb.AppendLine($"            {prop.Name} = dto.{prop.Name},");
        }

        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    public static System.Linq.IQueryable<{info.DtoName}> ProjectToDto(this System.Linq.IQueryable<{info.EntityName}> query)");
        sb.AppendLine("    {");
        sb.AppendLine($"        return query.Select(entity => new {info.DtoName}");
        sb.AppendLine("        {");

        foreach (var prop in info.Properties.Where(p => !p.IsReadOnly))
        {
            sb.AppendLine($"            {prop.Name} = entity.{prop.Name},");
        }

        sb.AppendLine("        });");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private class EntityInfo
    {
        public string EntityName { get; set; } = "";
        public string DtoName { get; set; } = "";
        public string Namespace { get; set; } = "";
        public ImmutableArray<PropertyInfo> Properties { get; set; }
        public ImmutableArray<string> UnmatchedExcludeNames { get; set; }
        public LocationInfo? UnmatchedExcludeLocation { get; set; }
    }

    private class PropertyInfo
    {
        public string Name { get; set; } = "";
        public string TypeName { get; set; } = "";
        public bool IsReadOnly { get; set; }
    }
}
