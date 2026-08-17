using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.DataProtection;

public static class BlazorBaseDataProtectionServiceCollectionExtensions
{
    /// <summary>
    /// Registers ASP.NET Core Data Protection for BlazorBase, optionally letting the host configure key
    /// management via <paramref name="configure"/>. Hosts running in a container or across a farm of nodes
    /// should use <paramref name="configure"/> to persist and harden the key ring, for example:
    /// <c>builder.PersistKeysToFileSystem(new DirectoryInfo("/keys")).SetApplicationName("MyApp")</c> — a
    /// shared/mounted volume so keys survive process restarts, and a stable application name so all nodes
    /// derive/share the same keys instead of a name based on the content-root path — plus
    /// <c>ProtectKeysWith*</c> to encrypt the keys at rest.
    /// Omitting <paramref name="configure"/> falls back to ASP.NET Core's default key management, which is
    /// NOT safe for containers (keys are ephemeral, in-memory, and lost on every restart) or multi-node farms
    /// (each node derives its own unshared keys, so data encrypted on one node cannot be decrypted on another).
    /// </summary>
    /// <param name="services">The service collection to add Data Protection to.</param>
    /// <param name="configure">
    /// Optional callback to configure the <see cref="IDataProtectionBuilder"/>, e.g. to persist and protect keys.
    /// </param>
    public static IServiceCollection AddBlazorBaseDataProtection(
        this IServiceCollection services,
        Action<IDataProtectionBuilder>? configure = null)
    {
        var builder = services.AddDataProtection();
        configure?.Invoke(builder);
        return services;
    }
}
