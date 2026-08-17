namespace BlazorBase.CRUD.Attributes;

/// <summary>
/// Marks an EF Core entity for automatic DTO and mapping generation by the source generator.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class BaseEntityAttribute : Attribute
{
    public string? DtoName { get; set; }

    public string[]? ExcludeProperties { get; set; }

    public bool IncludeNavigationProperties { get; set; }
}
