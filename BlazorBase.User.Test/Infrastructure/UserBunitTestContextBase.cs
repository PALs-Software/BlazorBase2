using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.User.Test.Infrastructure;

/// <summary>
/// Base bUnit context for the user-management component tests. Registers the Fluent UI services the
/// inputs depend on and runs JS interop in loose mode so Fluent UI's JS calls resolve to defaults
/// instead of failing the render.
/// </summary>
public abstract class UserBunitTestContextBase : BunitContext
{
    protected UserBunitTestContextBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddFluentUIComponents();
    }
}
