# BlazorBase.CRUD.Generators

A **Roslyn incremental source generator** that emits a DTO class and a static mapping-extensions class for every entity decorated with `[BaseEntity]`.

> Target framework: `netstandard2.0` (required for Roslyn analyzers) · No runtime dependency on the consuming project.

> With BlazorBase.CRUD's *Entity = Model* approach, DTOs are no longer required for standard CRUD scenarios. Use this generator only when an explicit DTO shape (different from the entity) is needed — e.g. for shaping API contracts, hiding navigation properties, or supplying a projection target for read-only queries.

---

## Table of Contents

- [Architecture & Overview](#architecture--overview)
- [Getting Started](#getting-started)
- [Reference: `[BaseEntity]`](#reference-baseentity)
- [What Gets Generated](#what-gets-generated)
- [Code Examples](#code-examples)

---

## Architecture & Overview

```
┌─────────────────────────────────────────────────────────┐
│  Compile-Time (during host build)                       │
│                                                         │
│  ┌──────────────────────────────┐                       │
│  │ ProductEntity.cs             │                       │
│  │  [BaseEntity(DtoName=...)]   │                       │
│  │  class ProductEntity { … }   │                       │
│  └──────────────┬───────────────┘                       │
│                 ▼                                       │
│   BaseDtoGenerator : IIncrementalGenerator              │
│     - ForAttributeWithMetadataName("…BaseEntity…")      │
│     - inspect public instance properties                │
│     - filter ExcludeProperties / navigation properties  │
│                 │                                       │
│                 ▼                                       │
│  ┌──────────────────────────────┐                       │
│  │ ProductDto.g.cs (emitted)    │                       │
│  │  partial class ProductDto    │                       │
│  │  static ProductEntity        │                       │
│  │     MappingExtensions        │                       │
│  └──────────────────────────────┘                       │
└─────────────────────────────────────────────────────────┘
```

The generator is **incremental** — only entities whose source changes are regenerated. It runs entirely at compile time; no runtime reflection or DI is involved.

---

## Getting Started

### 1. Reference the generator as an analyzer

In the project that owns the entity classes (e.g. `OmniLog.Web` or a shared models project):

```xml
<ItemGroup>
  <ProjectReference Include="..\Libs\BlazorBase.CRUD.Generators\BlazorBase.CRUD.Generators.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

> `OutputItemType="Analyzer"` is what tells the C# compiler to treat the project as a source generator. `ReferenceOutputAssembly="false"` prevents the analyzer's `netstandard2.0` assembly from being copied to the consumer's output.

### 2. Reference BlazorBase.CRUD (for the attribute)

`[BaseEntity]` lives in `BlazorBase.CRUD.Attributes`. The consuming project must reference `BlazorBase.CRUD`:

```xml
<ProjectReference Include="..\Libs\BlazorBase.CRUD\BlazorBase.CRUD.csproj" />
```

### 3. Decorate an entity

```csharp
using BlazorBase.CRUD.Attributes;

namespace MyApp.Data;

[BaseEntity(DtoName = "ProductDto", ExcludeProperties = ["InternalField"])]
public class ProductEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string InternalField { get; set; } = string.Empty;
}
```

### 4. Build the project

The DTO and mapping extensions are emitted to the obj/generator output (`ProductDto.g.cs`) and become part of the same assembly and namespace.

---

## Reference: `[BaseEntity]`

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class BaseEntityAttribute : Attribute
{
    public string?  DtoName { get; set; }
    public string[]? ExcludeProperties { get; set; }
    public bool      IncludeNavigationProperties { get; set; }
}
```

| Property | Default | Description |
|---|---|---|
| `DtoName` | `<EntityName>Dto` | Name of the generated DTO class |
| `ExcludeProperties` | `null` | Property names that must not appear on the DTO |
| `IncludeNavigationProperties` | `false` | When `false`, reference and collection navigations are skipped automatically |

> **Typo diagnostic (GEN-01).** An `ExcludeProperties` value that matches **no** property of the entity
> is reported as build warning **`BLAZORBASECRUD001`** (naming the value and the entity) instead of being
> silently ignored — previously a mistyped name (e.g. `"Naem"` for `"Name"`) left the intended property
> unexcluded and silently present in the generated DTO, a data-exposure risk. Fix the typo (or remove the
> stale name) to clear the warning.

### Navigation-property detection

A property is considered a navigation property and excluded by default when:
- its type is a `class` (not `string`) — **reference navigation**, or
- its type is `ICollection<T>`, `IList<T>`, or `List<T>` — **collection navigation**.

Set `IncludeNavigationProperties = true` to include them.

---

## What Gets Generated

For an entity `ProductEntity` with `DtoName = "ProductDto"` the generator emits a single file `ProductDto.g.cs`:

```csharp
// <auto-generated />
#nullable enable

namespace MyApp.Data;

public partial class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
}

public static class ProductEntityMappingExtensions
{
    public static ProductDto ToDto(this ProductEntity entity)
    {
        return new ProductDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Price = entity.Price,
        };
    }

    public static ProductEntity ToEntity(this ProductDto dto)
    {
        return new ProductEntity
        {
            Id = dto.Id,
            Name = dto.Name,
            Price = dto.Price,
        };
    }

    public static IQueryable<ProductDto> ProjectToDto(this IQueryable<ProductEntity> query)
    {
        return query.Select(entity => new ProductDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Price = entity.Price,
        });
    }
}
```

Notes:
- The DTO is `partial`, so you can extend it in a hand-written file.
- Read-only properties (no public setter) are emitted on the DTO but skipped from the mapping/projection bodies.
- `ProjectToDto` translates to a single SQL `SELECT` over the underlying `IQueryable` — no in-memory mapping.

---

## Code Examples

### Default DTO name

```csharp
[BaseEntity]
public class Customer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
```

Generates `CustomerDto` (default naming) with `Id`, `Name`, `Email`.

### Excluding sensitive fields

```csharp
[BaseEntity(ExcludeProperties = ["PasswordHash", "ApiKey"])]
public class Account
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}
```

The generated `AccountDto` contains only `Id` and `UserName`.

### Keeping navigation properties

```csharp
[BaseEntity(IncludeNavigationProperties = true)]
public class Order
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;

    public Customer? Customer { get; set; }
    public ICollection<OrderItem> Items { get; set; } = [];
}
```

The generated `OrderDto` now also has `Customer` and `Items` (typed as the same navigation types — you typically pair this with a Customer/`OrderItem` `[BaseEntity]` so the entire graph maps cleanly).

### Projection in a query

```csharp
public async Task<IReadOnlyList<ProductDto>> SearchAsync(string term, CancellationToken ct)
{
    return await DbContext.Products
        .Where(p => EF.Functions.Like(p.Name, $"%{term}%"))
        .OrderBy(p => p.Name)
        .ProjectToDto()
        .ToListAsync(ct);
}
```

### Extending the generated DTO (partial class)

```csharp
// Hand-written file — same namespace, same name
public partial class ProductDto
{
    public string FormattedPrice => Price.ToString("C2");
}
```

The generator emits a `partial class` so hand-written members can be added without touching generated code.
