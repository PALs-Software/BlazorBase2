using BlazorBase.CRUD.Core;

namespace BlazorBase.CRUD.Test.Infrastructure;

public sealed class StubAuditUserProvider(string? userId) : IAuditUserProvider
{
    private readonly string? UserId = userId;

    public string? GetCurrentUserId()
    {
        return UserId;
    }
}
