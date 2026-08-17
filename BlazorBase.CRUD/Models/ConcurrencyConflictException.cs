namespace BlazorBase.CRUD.Models;

/// <summary>
/// Thrown when a PATCH operation fails due to an optimistic concurrency conflict.
/// The entity was modified by another user since it was last loaded.
/// </summary>
public sealed class ConcurrencyConflictException(string message) : Exception(message);
