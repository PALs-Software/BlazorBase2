namespace BlazorBase.User.Server.Tokens;

/// <summary>
/// Opts an endpoint out of <see cref="BlazorBaseCsrfMiddleware"/>. Apply only to an endpoint that
/// must stay reachable cross-site, or one that carries its own proof of intent the way
/// <c>refresh</c>/<c>logout</c> do through <see cref="IAuthTokenTransport.IsRequestIntentionalAsync"/>.
/// Nothing carries this attribute by default.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class AllowBlazorBaseCsrfAttribute : Attribute;
