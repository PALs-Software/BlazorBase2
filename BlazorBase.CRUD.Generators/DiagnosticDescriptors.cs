using Microsoft.CodeAnalysis;

namespace BlazorBase.CRUD.Generators;

public static class DiagnosticDescriptors
{
    public const string UnmatchedExcludePropertyId = "BLAZORBASECRUD001";

    public static readonly DiagnosticDescriptor UnmatchedExcludeProperty = new(
        UnmatchedExcludePropertyId,
        title: "ExcludeProperties value does not match any property",
        messageFormat: "[BaseEntity] ExcludeProperties value '{0}' does not match any property of entity '{1}'",
        category: "BlazorBase.CRUD.Generators",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
