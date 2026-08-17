using BlazorBase.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace BlazorBase.Files.Server.Services;

/// <summary>
/// Extension methods to register the <c>BlazorBase.Files.Server</c> module with the dependency
/// injection container.
/// </summary>
public static class BlazorBaseFilesServerServiceCollectionExtensions
{
    /// <summary>
    /// Registers all BlazorBase.Files server-side services: options, filesystem storage,
    /// image service, default file-access authorizer (<see cref="OwnerScopedFileAccessAuthorizer"/>),
    /// HMAC token service, and the temporary file cleanup hosted service.
    /// </summary>
    /// <remarks>
    /// The host must call <c>AddDbContext&lt;TContext&gt;()</c> for its concrete context (which must
    /// expose a <c>DbSet&lt;BaseFile&gt;</c> and call <c>ApplyBaseFileConfiguration(modelBuilder)</c>
    /// in its <c>OnModelCreating</c> override) before calling
    /// <see cref="AddBlazorBaseFilesServer{TContext}"/>. This method bridges the base
    /// <see cref="DbContext"/> to the host's scoped <typeparamref name="TContext"/> registration, so
    /// <c>BaseFileControllerBase</c> and <see cref="OwnerScopedFileAccessAuthorizer"/> can resolve a
    /// scoped <see cref="DbContext"/> without any separate registration by the host. The abstract
    /// controller base (<c>BaseFileControllerBase</c>) must be derived by the host and discovered via
    /// ASP.NET Core's controller scanning.
    /// <para>
    /// Signing key configuration: add a <c>BlazorBaseFiles:TokenOptions:SigningKeyBase64</c>
    /// entry (base64-encoded, at least 32 random bytes) to application secrets or environment
    /// variables and bind it before calling this method, or use the <paramref name="configure"/>
    /// delegate:
    /// <code>
    /// services.Configure&lt;FileAccessTokenOptions&gt;(config.GetSection("BlazorBaseFiles:TokenOptions"));
    /// services.AddDbContext&lt;MyDbContext&gt;(options => options.UseSqlServer(connectionString));
    /// services.AddBlazorBaseFilesServer&lt;MyDbContext&gt;();
    /// </code>
    /// </para>
    /// </remarks>
    /// <typeparam name="TContext">The host's concrete <see cref="DbContext"/> type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional delegate to override default <see cref="BlazorBaseFileOptions"/>.</param>
    public static IServiceCollection AddBlazorBaseFilesServer<TContext>(
        this IServiceCollection services,
        Action<IBlazorBaseFileOptions>? configure = null)
        where TContext : DbContext
    {
        var fileOptions = new BlazorBaseFileOptions();
        configure?.Invoke(fileOptions);
        services.AddSingleton<IBlazorBaseFileOptions>(fileOptions);

        services.AddSingleton<IFileStorage, FileSystemFileStorage>();
        services.AddSingleton<IImageService, ImageSharpImageService>();
        services.AddScoped<IFileAccessAuthorizer, OwnerScopedFileAccessAuthorizer>();
        services.AddSingleton<IFileAccessTokenService, HmacFileAccessTokenService>();

        services.AddScoped<DbContext>(serviceProvider => serviceProvider.GetRequiredService<TContext>());

        services.AddSingleton<IValidateOptions<FileAccessTokenOptions>, FileAccessTokenOptionsValidator>();
        services.AddOptions<FileAccessTokenOptions>().ValidateOnStart();

        services.AddHostedService<TemporaryFileCleanupService>();

        return services;
    }

    /// <summary>
    /// Replaces the default <see cref="OwnerScopedFileAccessAuthorizer"/> with a
    /// host-supplied implementation, for example the insecure opt-in
    /// <see cref="AllowAuthenticatedFileAccessAuthorizer"/> or a custom per-host authorizer.
    /// Call after <see cref="AddBlazorBaseFilesServer{TContext}"/>.
    /// </summary>
    /// <remarks>
    /// Registered with a <strong>scoped</strong> lifetime so the authorizer may depend on
    /// per-request scoped services (for example a <c>DbContext</c>) without provoking a captive
    /// dependency. The consuming <c>BaseFileControllerBase</c> is itself scoped, so this is safe.
    /// </remarks>
    /// <typeparam name="TAuthorizer">The host's <see cref="IFileAccessAuthorizer"/> implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddBlazorBaseFileAccessAuthorizer<TAuthorizer>(
        this IServiceCollection services)
        where TAuthorizer : class, IFileAccessAuthorizer
    {
        services.Replace(ServiceDescriptor.Scoped<IFileAccessAuthorizer, TAuthorizer>());
        return services;
    }
}
