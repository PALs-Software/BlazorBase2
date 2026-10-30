using BlazorBase.Components.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.User.Test.Infrastructure;

/// <summary>
/// Base bUnit context for the user-management component tests. Registers the Fluent UI services the
/// inputs depend on the way an application does and runs JS interop in loose mode so Fluent UI's JS
/// calls resolve to defaults instead of failing the render.
/// </summary>
public abstract class UserBunitTestContextBase : BunitContext
{
    protected UserBunitTestContextBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazorBaseFluentUI();
    }
}
