namespace BlazorBase.CRUD.Models;

/// <summary>
/// Thrown by an <see cref="Core.IBaseDataProvider{TModel}"/> when a write (<c>PatchAsync</c> or
/// <c>DeleteAsync</c>) targets a row that does not exist or that the provider's query cannot see
/// (for example because of an EF Core global query filter). The generic CRUD endpoints map it to
/// <c>404 Not Found</c>.
/// </summary>
/// <remarks>
/// Derives from <see cref="InvalidOperationException"/> because that is what the providers threw
/// before this type existed, so callers that already catch <see cref="InvalidOperationException"/>
/// keep working unchanged.
/// </remarks>
public sealed class EntityNotFoundException(string message) : InvalidOperationException(message)
{
    /// <summary>
    /// Creates the exception for a row of <paramref name="entityType"/> addressed by <paramref name="id"/>.
    /// </summary>
    public static EntityNotFoundException For(Type entityType, object id)
        => new($"Entity of type {entityType.Name} with id '{id}' not found.");
}
