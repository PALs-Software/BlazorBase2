using System.Security.Claims;

namespace BlazorBase.Dashboard;

/// <summary>
/// What a part's visibility rule gets to decide with: the signed-in user, the service provider of the role center and a
/// cancellation token that is cancelled when the role center goes away.
/// </summary>
/// <param name="User">The current user; never null, anonymous when nobody is signed in.</param>
/// <param name="Services">The scoped service provider, for rules that need a policy service or a server call.</param>
/// <param name="CancellationToken">Cancelled when the role center is disposed.</param>
public sealed record DashboardPartContext(ClaimsPrincipal User, IServiceProvider Services, CancellationToken CancellationToken = default);
