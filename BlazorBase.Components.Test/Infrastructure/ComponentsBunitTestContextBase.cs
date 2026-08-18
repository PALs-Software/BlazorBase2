using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Components.Test.Infrastructure;

/// <summary>
/// Base bUnit context for the shared component tests. Registers the Fluent UI services the components
/// depend on and runs JS interop in loose mode so Fluent UI's JS calls resolve to defaults instead of
/// failing the render.
/// </summary>
public abstract class ComponentsBunitTestContextBase : BunitContext
{
    protected ComponentsBunitTestContextBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddFluentUIComponents();
    }
}
