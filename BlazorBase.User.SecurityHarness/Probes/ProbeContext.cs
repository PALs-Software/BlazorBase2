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
public sealed record ProbeContext(
    Uri BaseAddress,
    Func<HttpClient> HttpClientFactory,
    IServiceProvider Services,
    TimeProvider TimeProvider);
