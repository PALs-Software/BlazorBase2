namespace BlazorBase.CRUD.Core;

public interface IAuditUserProvider
{
    string? GetCurrentUserId();
}
