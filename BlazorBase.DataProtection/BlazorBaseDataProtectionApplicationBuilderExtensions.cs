using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.DataProtection;

public static class BlazorBaseDataProtectionApplicationBuilderExtensions
{
    /// <summary>
    /// Initializes the static <see cref="BlazorBaseDataProtection"/> provider so that <c>EncryptString</c> /
    /// <c>DecryptStringToInsecureString</c> extension methods can be used from contexts without dependency
    /// injection. Call once during application startup, after the service provider has been built (e.g.
    /// <c>app.Services.UseBlazorBaseDataProtection()</c>). Takes <see cref="IServiceProvider"/> rather than
    /// <c>IApplicationBuilder</c> so this project has no dependency on ASP.NET Core's web hosting types, which are
    /// unavailable to Blazor WebAssembly client projects that reference this one transitively via BlazorBase.CRUD.
    /// ASP.NET Core itself logs a warning when the resolved key ring is ephemeral (in-memory); hosts running in a
    /// container or across a farm of nodes must configure persistent, shared key storage via
    /// <c>AddBlazorBaseDataProtection(configure)</c> — see
    /// <c>Documentation/BlazorBase.DataProtection.md</c> for guidance.
    /// </summary>
    public static IServiceProvider UseBlazorBaseDataProtection(this IServiceProvider serviceProvider)
    {
        var provider = serviceProvider.GetRequiredService<IDataProtectionProvider>();
        BlazorBaseDataProtection.Initialize(provider);
        return serviceProvider;
    }
}
