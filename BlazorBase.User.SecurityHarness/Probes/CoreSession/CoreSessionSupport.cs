using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.User.SecurityHarness.Probes.CoreSession;

/// <summary>
/// Shared plumbing every core-session probe uses: seeding a user through the profile's own service
/// provider rather than through HTTP, sending a JSON request and capturing exactly what came back as
/// <see cref="ProbeEvidence"/>, and a couple of small response-shape helpers.
/// </summary>
internal static class CoreSessionSupport
{
    /// <summary>
    /// <see cref="JsonSerializerDefaults.Web"/>: camelCase, case-insensitive on read - matching what
    /// the controllers under test actually serialise, so a probe's own (de)serialisation is never the
    /// thing that produces a mismatch.
    /// </summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>A password that satisfies the framework's default policy (length 12, digit, upper, lower).</summary>
    public const string SeedPassword = "Correct-Horse-1";

    public static string UniqueEmail(string localPart) => $"{localPart}-{Guid.NewGuid():N}@harness.local";

    /// <summary>
    /// Creates a user directly through <see cref="UserManager{TUser}"/> against the profile's own
    /// service provider - never through <c>api/auth/setup</c>, so a login or token probe never
    /// depends on the login path it exists to isolate.
    /// </summary>
    public static async Task<HarnessUser> SeedUserAsync(ProbeContext context, string email, string password = SeedPassword, bool isActive = true)
    {
        using var scope = context.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<HarnessUser>>();

        var user = new HarnessUser
        {
            UserName = email,
            Email = email,
            DisplayName = email,
            IsActive = isActive,
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Could not seed user '{email}': {string.Join("; ", result.Errors.Select(error => error.Description))}");

        return user;
    }

    public static async Task RotateSecurityStampAsync(ProbeContext context, string userId)
    {
        using var scope = context.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<HarnessUser>>();
        var user = await userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException($"User '{userId}' vanished before its security stamp could be rotated.");
        await userManager.UpdateSecurityStampAsync(user);
    }

    /// <summary>
    /// Sends one JSON request and returns the response together with everything needed to describe
    /// it as evidence. <paramref name="body"/> is serialised as JSON unless it is already a
    /// <see cref="string"/>, in which case it travels verbatim as the request body - the shape
    /// <c>TOKEN-GARBAGE</c> needs to send bodies that are not valid JSON at all.
    /// </summary>
    public static async Task<ProbeExchange> SendAsync(HttpClient client, HttpMethod method, Uri baseAddress, string path, object? body = null, string? bearerToken = null, IEnumerable<KeyValuePair<string, string>>? headers = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, new Uri(baseAddress, path));

        if (bearerToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        if (headers is not null)
        {
            foreach (var header in headers)
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        string? requestBody = null;

        if (body is string rawJson)
        {
            requestBody = rawJson;
            request.Content = new StringContent(rawJson, System.Text.Encoding.UTF8, "application/json");
        }
        else if (body is not null)
        {
            requestBody = JsonSerializer.Serialize(body, Json);
            request.Content = JsonContent.Create(body, options: Json);
        }

        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        return new ProbeExchange(method, path, requestBody, response.StatusCode, responseBody, response.Headers);
    }

    public static bool TryDeserialize<T>(string json, out T? value)
    {
        try
        {
            value = JsonSerializer.Deserialize<T>(json, Json);
            return value is not null;
        }
        catch (JsonException)
        {
            value = default;
            return false;
        }
    }

    /// <summary>Logs in and returns the minted token pair, or <see langword="null"/> when login did not answer 200.</summary>
    public static async Task<(LoginResponse? Tokens, ProbeExchange Exchange)> LoginAsync(HttpClient client, Uri baseAddress, string email, string password, CancellationToken cancellationToken)
    {
        var exchange = await SendAsync(client, HttpMethod.Post, baseAddress, "api/auth/login", new LoginRequest { Email = email, Password = password }, cancellationToken: cancellationToken);
        TryDeserialize<LoginResponse>(exchange.ResponseBody, out var tokens);

        return (exchange.StatusCode == System.Net.HttpStatusCode.OK ? tokens : null, exchange);
    }

    /// <summary>
    /// Whether <paramref name="accessToken"/> authenticates against the harness's own minimal
    /// protected endpoint (<c>harness/protected-probe</c>, mapped by <see cref="Hosting.HarnessHostFactory"/>).
    /// </summary>
    public static async Task<ProbeExchange> CallProtectedEndpointAsync(HttpClient client, Uri baseAddress, string? accessToken, CancellationToken cancellationToken) =>
        await SendAsync(client, HttpMethod.Get, baseAddress, "harness/protected-probe", bearerToken: accessToken, cancellationToken: cancellationToken);
}

/// <summary>One request/response pair, kept exactly as it went over the wire so it can become <see cref="ProbeEvidence"/>.</summary>
internal sealed record ProbeExchange(HttpMethod Method, string Path, string? RequestBody, System.Net.HttpStatusCode StatusCode, string ResponseBody, HttpResponseHeaders ResponseHeaders)
{
    public ProbeEvidence ToEvidence(string description) => new(
        description,
        Request: RequestBody is null ? $"{Method} {Path}" : $"{Method} {Path}\n{RequestBody}",
        Response: $"{(int)StatusCode} {StatusCode}\n{ResponseBody}");
}
