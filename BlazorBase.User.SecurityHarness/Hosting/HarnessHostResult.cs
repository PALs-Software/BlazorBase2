using Microsoft.Extensions.Hosting;

namespace BlazorBase.User.SecurityHarness.Hosting;

/// <summary>The outcome of attempting to start one <see cref="HarnessProfile"/>'s Kestrel host.</summary>
public sealed class HarnessHostResult
{
    public required HarnessProfile Profile { get; init; }
    public required string Description { get; init; }
    public required HarnessHostStatus Status { get; init; }
    public Uri? BaseAddress { get; init; }
    public string? FailureMessage { get; init; }
    public IHost? Host { get; init; }
    public Func<HttpClient>? HttpClientFactory { get; init; }

    public static HarnessHostResult Started(HarnessProfile profile, string description, Uri baseAddress, IHost host, Func<HttpClient> httpClientFactory) =>
        new()
        {
            Profile = profile,
            Description = description,
            Status = HarnessHostStatus.Started,
            BaseAddress = baseAddress,
            Host = host,
            HttpClientFactory = httpClientFactory,
        };

    public static HarnessHostResult FailedToStart(HarnessProfile profile, string description, string failureMessage) =>
        new()
        {
            Profile = profile,
            Description = description,
            Status = HarnessHostStatus.FailedToStart,
            FailureMessage = failureMessage,
        };

    public static HarnessHostResult Unavailable(HarnessProfile profile, string description, string reason) =>
        new()
        {
            Profile = profile,
            Description = description,
            Status = HarnessHostStatus.Unavailable,
            FailureMessage = reason,
        };
}
