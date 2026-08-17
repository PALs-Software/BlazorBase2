using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;

namespace BlazorBase.CRUD.Endpoints;

/// <summary>
/// Maps dynamic CRUD endpoints for a given entity type using minimal APIs.
/// Supports PATCH-only updates, field-level security via <see cref="Attributes.CrudAccessAttribute"/>,
/// and optimistic concurrency.
/// </summary>
public static class BaseEndpointMapper
{
    public static IEndpointRouteBuilder MapBaseEndpoints<TModel>(
        this IEndpointRouteBuilder endpoints,
        string route,
        Action<BaseEndpointOptions<TModel>>? configure = null
    ) where TModel : class
    {
        var options = new BaseEndpointOptions<TModel>();
        configure?.Invoke(options);

        var prefix = $"{options.RoutePrefix.TrimEnd('/')}/{route}";

        var queryEndpoint = endpoints.MapPost($"{prefix}/query", async (
            BaseQuery query,
            IBaseDataProvider<TModel> provider,
            HttpContext httpContext,
            CancellationToken cancellationToken
        ) =>
        {
            if (!HasClassRight<TModel>(httpContext.User, CrudRights.Read))
                return Results.Forbid();

            var unreadableField = QueryFieldAccessValidator.FindUnreadableField(typeof(TModel), query, httpContext.User);

            if (unreadableField is not null)
                return Results.Forbid();

            var scopeFilter = options.UserFilter?.Invoke(httpContext.User);
            var result = await provider.GetListAsync(query, scopeFilter, cancellationToken);
            CrudResponseSanitizer.Strip(result.Items, httpContext.User);
            return Results.Ok(result);
        });

        var getEndpoint = endpoints.MapGet($"{prefix}/{{id}}", async (
            string id,
            string? select,
            IBaseDataProvider<TModel> provider,
            HttpContext httpContext,
            CancellationToken cancellationToken
        ) =>
        {
            if (!HasClassRight<TModel>(httpContext.User, CrudRights.Read))
                return Results.Forbid();

            var parsedId = ParseId<TModel>(id);
            IEnumerable<string>? selectFields = select?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var scopeFilter = options.UserFilter?.Invoke(httpContext.User);
            var result = await provider.GetByIdAsync(parsedId, selectFields, scopeFilter, cancellationToken);

            if (result is null)
                return Results.NotFound();

            CrudResponseSanitizer.Strip(result, httpContext.User);
            return Results.Ok(result);
        });

        var countEndpoint = endpoints.MapGet($"{prefix}/count", async (
            IBaseDataProvider<TModel> provider,
            HttpContext httpContext,
            CancellationToken cancellationToken
        ) =>
        {
            if (!HasClassRight<TModel>(httpContext.User, CrudRights.Read))
                return Results.Forbid();

            var scopeFilter = options.UserFilter?.Invoke(httpContext.User);
            var count = await provider.GetCountAsync(scopeFilter, cancellationToken);
            return Results.Ok(count);
        });

        var createEndpoint = endpoints.MapPost(prefix, async (
            TModel model,
            IBaseDataProvider<TModel> provider,
            HttpContext httpContext,
            CancellationToken cancellationToken
        ) =>
        {
            if (!HasClassRight<TModel>(httpContext.User, CrudRights.Insert))
                return Results.Forbid();

            var fieldError = ValidatePropertyRights<TModel>(model, httpContext.User, CrudRights.Insert);

            if (fieldError is not null)
                return fieldError;

            try
            {
                var result = await provider.CreateAsync(model, cancellationToken);
                CrudResponseSanitizer.Strip(result, httpContext.User);
                return Results.Created($"{prefix}/{GetId(result)}", result);
            }
            catch (BaseValidationException ex)
            {
                return Results.BadRequest(new { error = "validation", message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        });

        var patchEndpoint = endpoints.MapPatch($"{prefix}/{{id}}", async (
            string id,
            PatchModel patchModel,
            IBaseDataProvider<TModel> provider,
            HttpContext httpContext,
            CancellationToken cancellationToken
        ) =>
        {
            if (!HasClassRight<TModel>(httpContext.User, CrudRights.Modify))
                return Results.Forbid();

            var validationError = ValidateChangedFieldRights<TModel>(patchModel.ChangedFields, httpContext.User);

            if (validationError is not null)
                return validationError;

            var parsedId = ParseId<TModel>(id);

            var scopeFilter = options.UserFilter?.Invoke(httpContext.User);

            if (await IsOutOfScopeAsync(provider, parsedId, scopeFilter, cancellationToken))
                return Results.NotFound();

            try
            {
                var result = await provider.PatchAsync(parsedId, patchModel.ChangedFields, patchModel.ConcurrencyStamp, cancellationToken);
                CrudResponseSanitizer.Strip(result, httpContext.User);
                return Results.Ok(result);
            }
            catch (ConcurrencyConflictException ex)
            {
                return Results.Conflict(new { error = "ConcurrencyConflict", message = ex.Message });
            }
            catch (BaseValidationException ex)
            {
                return Results.BadRequest(new { error = "validation", message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        });

        var deleteEndpoint = endpoints.MapDelete($"{prefix}/{{id}}", async (
            string id,
            IBaseDataProvider<TModel> provider,
            HttpContext httpContext,
            CancellationToken cancellationToken
        ) =>
        {
            if (!HasClassRight<TModel>(httpContext.User, CrudRights.Delete))
                return Results.Forbid();

            var parsedId = ParseId<TModel>(id);

            var scopeFilter = options.UserFilter?.Invoke(httpContext.User);

            if (await IsOutOfScopeAsync(provider, parsedId, scopeFilter, cancellationToken))
                return Results.NotFound();

            try
            {
                await provider.DeleteAsync(parsedId, cancellationToken);
                return Results.NoContent();
            }
            catch (BaseValidationException ex)
            {
                return Results.BadRequest(new { error = "validation", message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        });

        if (options.RequireAuth)
        {
            var allEndpoints = new[] { queryEndpoint, getEndpoint, countEndpoint, createEndpoint, patchEndpoint, deleteEndpoint };

            foreach (var endpoint in allEndpoints)
            {
                if (options.AuthorizationPolicy is not null)
                    endpoint.RequireAuthorization(options.AuthorizationPolicy);
                else if (options.Roles is not null)
                    endpoint.RequireAuthorization(new Microsoft.AspNetCore.Authorization.AuthorizeAttribute { Roles = options.Roles });
                else
                    endpoint.RequireAuthorization();
            }
        }

        return endpoints;
    }

    #region Security

    private static bool HasClassRight<TModel>(ClaimsPrincipal user, CrudRights required)
    {
        var rights = CrudAccessResolver.EvaluateClass(typeof(TModel), user);
        return (rights & required) == required;
    }

    private static IResult? ValidateChangedFieldRights<TModel>(Dictionary<string, object?> changedFields, ClaimsPrincipal user)
    {
        foreach (var fieldName in changedFields.Keys)
        {
            var rules = CrudAccessResolver.GetPropertyRules(typeof(TModel), fieldName);

            if (rules.Count == 0)
                continue;

            if (CrudAccessResolver.IsAlwaysExcluded(typeof(TModel), fieldName))
                return Results.BadRequest(new { error = "InvalidField", field = fieldName, message = $"Field '{fieldName}' cannot be modified." });

            var effective = CrudAccessResolver.EvaluateProperty(typeof(TModel), fieldName, user);

            if ((effective & CrudRights.Modify) != CrudRights.Modify)
                return Results.Json(
                    new { error = "Forbidden", field = fieldName, message = $"You do not have permission to edit '{fieldName}'." },
                    statusCode: StatusCodes.Status403Forbidden);
        }

        return null;
    }

    private static IResult? ValidatePropertyRights<TModel>(TModel model, ClaimsPrincipal user, CrudRights required)
    {
        foreach (var (propertyName, rules) in CrudAccessResolver.GetPropertyRules(typeof(TModel)))
        {
            if (rules.Count == 0)
                continue;

            var property = typeof(TModel).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

            if (property is null)
                continue;

            var value = property.GetValue(model);

            if (IsDefaultValue(value, property.PropertyType))
                continue;

            if (CrudAccessResolver.IsAlwaysExcluded(typeof(TModel), propertyName))
                return Results.BadRequest(new { error = "InvalidField", field = propertyName, message = $"Field '{propertyName}' cannot be set." });

            var effective = CrudAccessResolver.EvaluateProperty(typeof(TModel), propertyName, user);

            if ((effective & required) != required)
                return Results.Json(
                    new { error = "Forbidden", field = propertyName, message = $"You do not have permission to set '{propertyName}'." },
                    statusCode: StatusCodes.Status403Forbidden);
        }

        return null;
    }

    private static bool IsDefaultValue(object? value, Type propertyType)
    {
        if (value is null)
            return true;

        if (!propertyType.IsValueType)
            return false;

        var defaultValue = Activator.CreateInstance(propertyType);
        return Equals(value, defaultValue);
    }

    #endregion

    #region Helpers

    private static object ParseId<TModel>(string id)
    {
        var idProperty = typeof(TModel).GetProperty("Id");

        if (idProperty is null)
            return id;

        var idType = idProperty.PropertyType;

        if (idType == typeof(Guid))
            return Guid.Parse(id);

        if (idType == typeof(int))
            return int.Parse(id);

        if (idType == typeof(long))
            return long.Parse(id);

        return id;
    }

    private static object? GetId<TModel>(TModel model)
    {
        return typeof(TModel).GetProperty("Id")?.GetValue(model);
    }

    private static async Task<bool> IsOutOfScopeAsync<TModel>(
        IBaseDataProvider<TModel> provider,
        object parsedId,
        Expression<Func<TModel, bool>>? scopeFilter,
        CancellationToken cancellationToken) where TModel : class
    {
        if (scopeFilter is null)
            return false;

        return await provider.GetByIdAsync(parsedId, new[] { "Id" }, scopeFilter, cancellationToken) is null;
    }

    #endregion
}
