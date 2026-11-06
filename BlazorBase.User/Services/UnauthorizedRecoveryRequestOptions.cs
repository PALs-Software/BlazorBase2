namespace BlazorBase.User.Services;

/// <summary>
/// Request options that tell the handlers of one client chain who answers a 401.
/// </summary>
/// <remarks>
/// <see cref="AuthTokenHandler"/> sets <see cref="HandledByAuthTokenHandler"/> to <c>true</c> on a request
/// whose 401 it will answer itself: it holds a refresh token and the request is not an auth endpoint.
/// <see cref="SessionExpiryHandler"/> then leaves that 401 alone. Without the marker it would drop the
/// shared session to anonymous and clear the stored tokens before the refresh ran, so a 401 that is
/// recovered a moment later would still show the components an anonymous user, and a refresh that fails
/// for network reasons would have wiped the tokens it meant to keep. The marker travels on the request,
/// so the outcome does not depend on the order in which the two handlers are chained. It is internal
/// because both handlers live in this assembly.
/// </remarks>
internal static class UnauthorizedRecoveryRequestOptions
{
    public static readonly HttpRequestOptionsKey<bool> HandledByAuthTokenHandler =
        new("BlazorBase.User.UnauthorizedHandledByAuthTokenHandler");
}
