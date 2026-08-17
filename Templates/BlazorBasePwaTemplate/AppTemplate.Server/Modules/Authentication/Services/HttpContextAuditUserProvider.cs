using System.Security.Claims;
using BlazorBase.CRUD.Core;

namespace AppTemplate.Server.Modules.Authentication.Services;

/// <summary>
/// Names the user that BlazorBase writes into the <see cref="AuditModel"/> CreatedBy and ModifiedBy
/// fields, taken from the authenticated principal of the request being served.
/// </summary>
/// <remarks>
/// The framework resolves this seam optionally: without a registration every audited row is saved
/// with a null author instead of failing, so the omission is silent. Any entity deriving from
/// <see cref="AuditModel"/> needs it.
/// </remarks>
public class HttpContextAuditUserProvider(IHttpContextAccessor httpContextAccessor) : IAuditUserProvider
{
    #region Injects

    private readonly IHttpContextAccessor HttpContextAccessor = httpContextAccessor;

    #endregion

    public string? GetCurrentUserId()
        => HttpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
}
