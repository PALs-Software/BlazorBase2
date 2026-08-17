using BlazorBase.CRUD.Components.CustomProperties;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.CRUD.Test.Infrastructure;

/// <summary>
/// Base bUnit context for component tests. Registers the Fluent UI services the CRUD
/// components depend on and puts JS interop into loose mode so Fluent UI's JS calls
/// resolve to defaults instead of failing the render. Also clears the static custom-property
/// resolution caches so the registered component set never leaks between tests.
/// </summary>
public abstract class BunitTestContextBase : BunitContext
{
    protected BunitTestContextBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddFluentUIComponents();

        CustomPropertyResolutionCache.ResetForTests();
    }
}
