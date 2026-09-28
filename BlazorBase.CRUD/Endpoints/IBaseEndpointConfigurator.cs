namespace BlazorBase.CRUD.Endpoints;

/// <summary>
/// Resolved from <see cref="Microsoft.AspNetCore.Routing.IEndpointRouteBuilder.ServiceProvider"/> by
/// <see cref="BaseEndpointMapper.MapBaseEndpoints{TModel}"/> and applied to every mapping of
/// <typeparamref name="TEntity"/>, whether reached through the assembly-scanning
/// <c>MapBlazorBaseCrudEndpoints(assembly)</c> overload or the typed
/// <c>MapBlazorBaseCrudEndpoints&lt;TEntity&gt;(route, configure)</c> overload.
/// </summary>
/// <remarks>
/// Register at most one implementation per entity type in DI. A host that needs a scope filter —
/// or any other option — applied regardless of which overload maps the endpoint registers a
/// configurator instead of relying on a call-site <c>configure</c> action, which the scanning
/// overload has no way to supply at all.
/// </remarks>
public interface IBaseEndpointConfigurator<TEntity> where TEntity : class
{
    /// <summary>
    /// Applies this configurator's settings to <paramref name="options"/>. Runs after any
    /// explicit <c>configure</c> action passed to the typed <c>MapBlazorBaseCrudEndpoints</c>
    /// overload — see <see cref="BaseEndpointMapper.MapBaseEndpoints{TModel}"/> for the exact
    /// ordering and why it runs last.
    /// </summary>
    void Configure(BaseEndpointOptions<TEntity> options);
}
