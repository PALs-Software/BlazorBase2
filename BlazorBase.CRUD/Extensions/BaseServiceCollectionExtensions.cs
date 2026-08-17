using System.Reflection;
using BlazorBase.CRUD.Attributes;
using BlazorBase.CRUD.Components.CustomProperties;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.DataProviders;
using BlazorBase.CRUD.Events;
using BlazorBase.CRUD.Interceptors;
using BlazorBase.CRUD.Security;
using BlazorBase.CRUD.Services;
using BlazorBase.CRUD.Validation;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace BlazorBase.CRUD.Extensions;

public static class BaseServiceCollectionExtensions
{
    public static IServiceCollection AddBlazorBaseCrud(this IServiceCollection services)
    {
        services.AddScoped(typeof(BaseValidationService<>));
        return services;
    }

    public static IServiceCollection AddBlazorBaseCrud<TAuditUserProvider>(this IServiceCollection services)
        where TAuditUserProvider : class, IAuditUserProvider
    {
        services.AddScoped<IAuditUserProvider, TAuditUserProvider>();
        return services.AddBlazorBaseCrud();
    }

    /// <summary>
    /// Registers the interactive UI services the CRUD components depend on (currently the
    /// <see cref="IConfirmationService"/>, backed by <see cref="FluentUiConfirmationService"/> which
    /// needs the FluentUI <c>IDialogService</c>). Call this only from a host that actually renders the
    /// CRUD components interactively — the WASM/MAUI client or a Blazor Server interactive host — after
    /// <c>AddFluentUIComponents()</c>. A pure hosting backend that never renders the components (for
    /// example the hosted-WASM server) must not call this and therefore needs no FluentUI
    /// <c>IDialogService</c>. The registration is a <c>TryAdd</c>, so a host can override
    /// <see cref="IConfirmationService"/> with its own implementation beforehand.
    /// </summary>
    public static IServiceCollection AddBlazorBaseCrudComponents(this IServiceCollection services)
    {
        services.TryAddScoped<IConfirmationService, FluentUiConfirmationService>();
        return services;
    }

    public static IServiceCollection AddBaseDbContext<TContext>(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext)
        where TContext : DbContext
    {
        services.AddScoped<IBaseDbInterceptor, BaseSaveChangesInterceptor>();

        services.AddDbContext<TContext>((sp, options) =>
        {
            configureDbContext(options);
            options.AddInterceptors(sp.GetServices<IBaseDbInterceptor>());
        });
        return services;
    }

    /// <summary>
    /// Registers a hosted service that validates at startup that every role referenced by
    /// [CrudAccess] on a [BaseCrud] entity exists in the Identity role store. Throws
    /// InvalidOperationException with the list of unknown roles when validation fails.
    /// </summary>
    public static IServiceCollection AddCrudAccessRoleValidation<TRole>(
        this IServiceCollection services,
        Assembly? assembly = null) where TRole : class
    {
        var scanAssembly = assembly ?? Assembly.GetCallingAssembly();
        services.AddSingleton<IHostedService>(sp => new CrudAccessRoleValidator<TRole>(sp, scanAssembly));
        return services;
    }

    /// <summary>
    /// Scans the given assembly (or calling assembly) for entity types decorated with
    /// [BaseCrud] and registers a DbContextBaseDataProvider for each.
    /// </summary>
    public static IServiceCollection AddBlazorBaseCrudServer<TContext>(
        this IServiceCollection services,
        Assembly? assembly = null)
        where TContext : DbContext
    {
        var scanAssembly = assembly ?? Assembly.GetCallingAssembly();

        foreach (var entityType in FindBaseCrudTypes(scanAssembly))
        {
            var providerInterface = typeof(IBaseDataProvider<>).MakeGenericType(entityType);
            var providerImpl = typeof(DbContextBaseDataProvider<>).MakeGenericType(entityType);

            services.AddScoped(providerInterface, sp =>
            {
                var dbContext = sp.GetRequiredService<TContext>();
                var interceptorType = typeof(IBaseDataInterceptor<>).MakeGenericType(entityType);
                var enumerableType = typeof(IEnumerable<>).MakeGenericType(interceptorType);
                var interceptors = sp.GetService(enumerableType);

                return Activator.CreateInstance(
                    providerImpl,
                    dbContext,
                    interceptors,
                    null)!;
            });
        }

        return services;
    }

    /// <summary>
    /// Scans the given assembly (or calling assembly) for entity types decorated with
    /// [BaseCrud] and registers an HttpBaseDataProvider for each.
    /// </summary>
    public static IServiceCollection AddBlazorBaseCrudClient(
        this IServiceCollection services,
        Action<HttpClient> configureClient,
        Assembly? assembly = null,
        Action<IHttpClientBuilder>? configureHttpClientBuilder = null)
    {
        return services.AddBlazorBaseCrudClient(
            (_, client) => configureClient(client),
            assembly ?? Assembly.GetCallingAssembly(),
            configureHttpClientBuilder);
    }

    public static IServiceCollection AddBlazorBaseCrudClient(
        this IServiceCollection services,
        Action<IServiceProvider, HttpClient> configureClient,
        Assembly? assembly = null,
        Action<IHttpClientBuilder>? configureHttpClientBuilder = null)
    {
        var scanAssembly = assembly ?? Assembly.GetCallingAssembly();

        foreach (var entityType in FindBaseCrudTypes(scanAssembly))
        {
            var attr = entityType.GetCustomAttribute<BaseCrudAttribute>()!;
            var providerInterface = typeof(IBaseDataProvider<>).MakeGenericType(entityType);
            var providerImpl = typeof(HttpBaseDataProvider<>).MakeGenericType(entityType);

            var httpClientBuilder = services.AddHttpClient(entityType.FullName!, configureClient);
            configureHttpClientBuilder?.Invoke(httpClientBuilder);

            services.AddScoped(providerInterface, sp =>
            {
                var clientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = clientFactory.CreateClient(entityType.FullName!);
                httpClient.BaseAddress = new Uri(
                    httpClient.BaseAddress!,
                    attr.Route.TrimStart('/') + "/");

                return Activator.CreateInstance(providerImpl, httpClient)!;
            });
        }

        return services;
    }

    public static IServiceCollection AddBaseHttpDataProvider<TModel>(
        this IServiceCollection services,
        string apiRoute)
        where TModel : class
    {
        services.AddHttpClient<IBaseDataProvider<TModel>, HttpBaseDataProvider<TModel>>(client =>
        {
            client.BaseAddress = new Uri(apiRoute);
        });
        return services;
    }

    public static IServiceCollection AddBaseHttpDataProvider<TModel>(
        this IServiceCollection services,
        Action<HttpClient> configureClient)
        where TModel : class
    {
        services.AddHttpClient<IBaseDataProvider<TModel>, HttpBaseDataProvider<TModel>>(configureClient);
        return services;
    }

    public static IServiceCollection AddBaseDataInterceptor<TModel, TInterceptor>(this IServiceCollection services)
        where TModel : class
        where TInterceptor : class, IBaseDataInterceptor<TModel>
    {
        services.AddScoped<IBaseDataInterceptor<TModel>, TInterceptor>();
        return services;
    }

    public static IServiceCollection AddBaseValidator<TModel, TValidator>(this IServiceCollection services)
        where TModel : class
        where TValidator : class, IBaseValidator<TModel>
    {
        services.AddScoped<IBaseValidator<TModel>, TValidator>();
        return services;
    }

    /// <summary>
    /// Registers a custom card input component for the generic CRUD card. The component opts in per
    /// property via <see cref="IBaseCustomPropertyInput.CanHandle"/>. Multiple registrations are
    /// allowed; the first whose <c>CanHandle</c> returns true (registration order) wins, and the
    /// per-field <c>EditorTemplate</c>/<c>DisplayTemplate</c> still takes precedence over all of them.
    /// </summary>
    public static IServiceCollection AddBlazorBaseCustomInput<TComponent>(this IServiceCollection services)
        where TComponent : class, IComponent, IBaseCustomPropertyInput
    {
        services.AddScoped<IBaseCustomPropertyInput, TComponent>();
        return services;
    }

    /// <summary>
    /// Registers a custom list-cell display component for the generic CRUD list. The component opts
    /// in per property via <see cref="IBaseCustomPropertyDisplay.CanHandle"/>. Multiple registrations
    /// are allowed; the first whose <c>CanHandle</c> returns true (registration order) wins.
    /// </summary>
    public static IServiceCollection AddBlazorBaseCustomDisplay<TComponent>(this IServiceCollection services)
        where TComponent : class, IComponent, IBaseCustomPropertyDisplay
    {
        services.AddScoped<IBaseCustomPropertyDisplay, TComponent>();
        return services;
    }

    internal static IEnumerable<Type> FindBaseCrudTypes(Assembly assembly)
    {
        return assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                && t.GetCustomAttribute<BaseCrudAttribute>() is not null);
    }
}
