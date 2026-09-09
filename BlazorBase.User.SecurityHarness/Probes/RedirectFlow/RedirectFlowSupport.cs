using System.Net;
using System.Security.Cryptography.X509Certificates;
using BlazorBase.User.SecurityHarness.IdentityProvider;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.User.SecurityHarness.Probes.RedirectFlow;

/// <summary>
/// Shared plumbing every redirect-flow probe uses: seeding the local user the fake provider's
/// authorization endpoint always authenticates as, and manually chasing the multi-hop redirect
/// chain a real browser would follow automatically - <c>challenge</c> to the fake provider's
/// authorize endpoint, its redirect back to the application's own OIDC callback path, and that
/// callback's redirect to <c>complete</c> - since the harness's probe client never follows a
/// redirect automatically (see <see cref="Hosting.HarnessHostFactory.CreateProbeHttpClient"/>) and
/// a probe needs to see every hop to assert on it.
/// </summary>
internal static class RedirectFlowSupport
{
    private const int MaxHops = 10;

    /// <summary>
    /// Seeds a local user whose username is <see cref="FakeIdentityProviderHost.RedirectFlowSubject"/>
    /// - the fixed subject the fake provider's authorization endpoint always authenticates as -
    /// so <c>StrictSubjectExternalUserResolver</c> can resolve it. Without this, every probe that
    /// expects the redirect flow to complete successfully would see a 401 for "no such local user"
    /// instead of the outcome it is actually testing, the same trap step 5 found for the
    /// token-exchange flow.
    /// </summary>
    public static Task EnsureResolvableSubjectAsync(ProbeContext context) =>
        CoreSessionSupport.SeedUserAsync(context, FakeIdentityProviderHost.RedirectFlowSubject);

    /// <summary>
    /// Builds an <see cref="HttpClient"/> pinned to this run's certificate thumbprint, unlike
    /// <see cref="ProbeContext.HttpClientFactory"/>, which is unpinned for the <c>External</c>
    /// profile's own plain-HTTP app host - a redirect-flow probe's client must also trust the fake
    /// identity provider's HTTPS endpoint directly, since it follows the browser's own hop there.
    /// </summary>
    public static HttpClient CreateClient(ProbeContext context)
    {
        var certificate = context.Services.GetRequiredService<X509Certificate2>();
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback = (_, presentedCertificate, _, _) =>
                presentedCertificate is not null && string.Equals(presentedCertificate.Thumbprint, certificate.Thumbprint, StringComparison.OrdinalIgnoreCase),
        };

        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
    }

    /// <summary>
    /// Follows the redirect chain starting at <c>GET api/auth/external/challenge</c>, stopping once a
    /// hop answers something other than a redirect (or the hop limit is hit) - a genuinely completed
    /// flow ends on <c>complete</c>'s own final redirect or 401.
    /// </summary>
    public static async Task<RedirectChainResult> RunChallengeAsync(HttpClient client, Uri baseAddress, string? returnUrl, CancellationToken cancellationToken)
    {
        var path = returnUrl is null ? "api/auth/external/challenge" : $"api/auth/external/challenge?returnUrl={Uri.EscapeDataString(returnUrl)}";

        return await FollowAsync(client, new Uri(baseAddress, path), cancellationToken);
    }

    /// <summary>Sends one uncoupled <c>challenge</c> request without following its redirect - for a rate-limit probe that floods the endpoint.</summary>
    public static Task<RedirectHop> ChallengeOnceAsync(HttpClient client, Uri baseAddress, CancellationToken cancellationToken) =>
        SendHopAsync(client, new Uri(baseAddress, "api/auth/external/challenge"), cancellationToken);

    private static async Task<RedirectChainResult> FollowAsync(HttpClient client, Uri startUri, CancellationToken cancellationToken)
    {
        var hops = new List<RedirectHop>();
        var hop = await SendHopAsync(client, startUri, cancellationToken);
        hops.Add(hop);

        while (IsRedirect(hop.Exchange) && hops.Count < MaxHops)
        {
            var location = ResolveLocation(hop);

            if (location is null)
                break;

            hop = await SendHopAsync(client, location, cancellationToken);
            hops.Add(hop);
        }

        return new RedirectChainResult(hops);
    }

    private static async Task<RedirectHop> SendHopAsync(HttpClient client, Uri requestUri, CancellationToken cancellationToken)
    {
        var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Get, requestUri, string.Empty, cancellationToken: cancellationToken);
        return new RedirectHop(requestUri, exchange);
    }

    private static bool IsRedirect(ProbeExchange exchange) =>
        exchange.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.SeeOther or HttpStatusCode.Moved or HttpStatusCode.RedirectKeepVerb;

    private static Uri? ResolveLocation(RedirectHop hop)
    {
        if (!hop.Exchange.ResponseHeaders.TryGetValues("Location", out var values))
            return null;

        var location = values.FirstOrDefault();

        if (location is null)
            return null;

        return Uri.TryCreate(location, UriKind.Absolute, out var absolute) ? absolute : new Uri(hop.RequestUri, location);
    }
}

/// <summary>One request/response pair in a followed redirect chain.</summary>
internal sealed record RedirectHop(Uri RequestUri, ProbeExchange Exchange);

/// <summary>
/// Every hop a followed redirect chain produced, in order. <see cref="Final"/> is the last one - the
/// chain's actual outcome, either a non-redirect response or the hop the loop gave up on.
/// </summary>
internal sealed record RedirectChainResult(IReadOnlyList<RedirectHop> Hops)
{
    public RedirectHop Final => Hops[^1];

    public ProbeEvidence ToEvidence(string description) => new(
        description,
        Request: string.Join("\n", Hops.Select((hop, index) => $"Hop {index + 1}: GET {hop.RequestUri}")),
        Response: string.Join("\n", Hops.Select((hop, index) => $"Hop {index + 1}: {(int)hop.Exchange.StatusCode} {hop.Exchange.StatusCode}" +
            (hop.Exchange.ResponseHeaders.TryGetValues("Location", out var location) ? $" -> {location.FirstOrDefault()}" : string.Empty))));
}
