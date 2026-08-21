# BlazorBase.Localization

.NET library (net10.0). Composition helpers for `IStringLocalizer`. One package reference
(`Microsoft.Extensions.Localization`), no Blazor, no FluentUI, no static web assets.

It sits at the bottom of the graph on purpose. Localizing a validation message thrown from an EF Core
interceptor, or a `ProblemDetails` returned by a controller, is a server concern, and a server library
should not have to reference a Razor Class Library — with its FluentUI dependency and its `wwwroot` —
to compose two localizers.

---

## Types

| Type | Purpose |
|---|---|
| `ChainedLocalizer` | Walks a list of localizers in priority order and returns the first hit where the key was actually found. |
| `EmptyLocalizer` | Singleton fallback that always reports `ResourceNotFound`, returning the key as its own value. |

Both are built around `LocalizedString.ResourceNotFound` rather than around null or empty checks: a
resource that legitimately resolves to an empty string is a hit, and a key that resolves to itself is
not. Anything that inspects the value instead gets one of those two backwards.

```csharp
var localizer = new ChainedLocalizer([
    perRequestOverrides,                    // wins when it has the key
    factory.Create(typeof(OrderResources)), // the entity's own resources
    factory.Create(typeof(AuditModel)),     // shared captions from the base type
]);

var caption = localizer["DueDate"];         // first localizer that actually resolves it
```

`EmptyLocalizer.Instance` closes the chain when no factory is registered at all, so callers never have
to null-check a localizer:

```csharp
IStringLocalizer localizer = factory is null
    ? EmptyLocalizer.Instance
    : new ChainedLocalizer(chain);
```

---

## Who uses it

`BlazorBase.CRUD`'s `LocalizerResolver` builds both of its chains from these two types — the property
chain (explicit localizer → model type → its base types → raw key) and the framework chain for
built-in chrome strings. See [BlazorBase.CRUD.md](BlazorBase.CRUD.md) for the resolution order the CRUD
components rely on.

Nothing here is CRUD-specific. A mailing template that needs a per-tenant override in front of the
default resources, or a controller that wants a caller-supplied localizer to win over its own, uses the
same two types without taking a UI dependency.
