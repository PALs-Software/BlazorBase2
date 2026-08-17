using BlazorBase.CRUD.Attributes;
using BlazorBase.CRUD.Endpoints;
using Microsoft.AspNetCore.Routing;
using System.Reflection;

namespace BlazorBase.CRUD.Extensions;

public static class BaseApplicationBuilderExtensions
{
    public static IEndpointRouteBuilder MapBlazorBaseCrudEndpoints<TModel>(
        this IEndpointRouteBuilder endpoints,
        string route,
        Action<BaseEndpointOptions<TModel>>? configure = null)
        where TModel : class
    {
        endpoints.MapBaseEndpoints<TModel>(route, configure);
        return endpoints;
    }

    /// <summary>
    /// Scans the given assembly (or calling assembly) for entity types decorated with
    /// [BaseCrud] and maps CRUD endpoints for each.
    /// </summary>
    public static IEndpointRouteBuilder MapBlazorBaseCrudEndpoints(
        this IEndpointRouteBuilder endpoints,
        Assembly? assembly = null)
    {
        var scanAssembly = assembly ?? Assembly.GetCallingAssembly();
        var mapMethod = typeof(BaseEndpointMapper)
            .GetMethod(nameof(BaseEndpointMapper.MapBaseEndpoints))!;

        foreach (var entityType in BaseServiceCollectionExtensions.FindBaseCrudTypes(scanAssembly))
        {
            var attr = entityType.GetCustomAttribute<BaseCrudAttribute>()!;
            var genericMethod = mapMethod.MakeGenericMethod(entityType);
            genericMethod.Invoke(null, [endpoints, attr.Route, null]);
        }

        return endpoints;
    }
}
