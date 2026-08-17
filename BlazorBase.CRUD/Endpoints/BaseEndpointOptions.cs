using System.Linq.Expressions;
using System.Security.Claims;

namespace BlazorBase.CRUD.Endpoints;

/// <summary>
/// Configuration for auto-registered CRUD endpoints per entity type.
/// </summary>
public class BaseEndpointOptions<TEntity> where TEntity : class
{
    public string RoutePrefix { get; set; } = "api/base";

    public string? AuthorizationPolicy { get; set; }

    public string? Roles { get; set; }

    public bool RequireAuth { get; set; } = true;

    public Func<ClaimsPrincipal, Expression<Func<TEntity, bool>>>? UserFilter { get; set; }
}
