namespace BlazorBase.User.SecurityHarness.Probes;

/// <summary>
/// What a probe needs to attack one running <see cref="Hosting.HarnessProfile"/> host: where it
/// lives, how to talk to it, and how to reach behind it when a probe needs to seed data directly.
/// </summary>
/// <param name="BaseAddress">The profile's own base address, loopback and operating-system assigned.</param>
/// <param name="HttpClientFactory">
/// Creates a fresh <see cref="HttpClient"/> that does not follow redirects and starts with no
/// cookies - a probe that wants a shared cookie jar across requests keeps one client instance
/// itself rather than relying on this factory to provide one.
/// </param>
/// <param name="Services">
/// A scoped service provider into the profile's own host, so a probe can seed a user or read the
/// database directly instead of only driving the host through HTTP.
/// </param>
/// <param name="TimeProvider">The <see cref="System.TimeProvider"/> the profile's host was built with.</param>
/// <param name="StartIsolatedHostAsync">
/// Starts a private, differently-configured instance of this same profile - its own port, its own
/// database file - so a probe that needs a shortened token lifetime, a lowered rate-limit permit
/// count, or a shortened lockout window can ask for it through <c>AuthTokens:*</c> configuration
/// overrides without changing what any other probe measures, and a rate-limit or lockout probe never
/// leaves its budget half-spent for whichever probe runs after it. The caller disposes the result to
/// stop that private host.
/// </param>
public sealed record ProbeContext(
    Uri BaseAddress,
    Func<HttpClient> HttpClientFactory,
    IServiceProvider Services,
    TimeProvider TimeProvider,
    Func<IReadOnlyDictionary<string, string?>, CancellationToken, Task<IsolatedProbeHost>> StartIsolatedHostAsync);

/// <summary>
/// A private <see cref="Hosting.HarnessHostFactory"/>-started host handed to one probe, alongside how
/// to stop it again. See <see cref="ProbeContext.StartIsolatedHostAsync"/>.
/// </summary>
public sealed class IsolatedProbeHost(ProbeContext context, Func<Task> stopAsync) : IAsyncDisposable
{
    public ProbeContext Context { get; } = context;

    public async ValueTask DisposeAsync() => await stopAsync();
}
