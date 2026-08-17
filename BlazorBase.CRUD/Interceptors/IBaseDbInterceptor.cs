using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BlazorBase.CRUD.Interceptors;

/// <summary>
/// Marker interface for EF Core interceptors that should be automatically attached
/// to the BlazorBase DbContext registration. Inherit from this instead of <see cref="IInterceptor"/>
/// directly to avoid colliding with other <see cref="IInterceptor"/> registrations
/// the host application may use for unrelated purposes.
/// </summary>
public interface IBaseDbInterceptor : IInterceptor;
