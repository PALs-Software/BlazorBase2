using System.Net;
using BlazorBase.User.Services;
using BlazorBase.User.Wasm;
using Xunit;

namespace BlazorBase.User.Test.Wasm;

/// <summary>
/// Without the origin check, an absolute or attacker-influenced request URL would carry this
/// application's session cookies and identity-bound antiforgery token to whatever origin it names.
/// </summary>
public sealed class CsrfHeaderHandlerTests
{
    private static readonly HttpRequestOptionsKey<IDictionary<string, object>> FetchOptionsKey = new("WebAssemblyFetchOptions");

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }

    private static HttpMessageInvoker CreateInvoker(Uri? apiOrigin)
    {
        var tokenProvider = new CsrfTokenProvider();
        tokenProvider.Set("the-token");

        var options = new CsrfTokenOptions();
        SetApiOrigin(options, apiOrigin);

        return new HttpMessageInvoker(new CsrfHeaderHandler(tokenProvider, options)
        {
            InnerHandler = new StubHandler()
        });
    }

    private static void SetApiOrigin(CsrfTokenOptions options, Uri? apiOrigin)
    {
        var setter = typeof(CsrfTokenOptions).GetProperty(nameof(CsrfTokenOptions.ApiOrigin))!.GetSetMethod(nonPublic: true)!;
        setter.Invoke(options, [apiOrigin]);
    }

    private static string? Credentials(HttpRequestMessage request)
        => request.Options.TryGetValue(FetchOptionsKey, out var fetchOptions) && fetchOptions.TryGetValue("credentials", out var value)
            ? value as string
            : null;

    [Fact]
    public async Task A_write_to_the_api_origin_carries_credentials_and_the_token()
    {
        using var invoker = CreateInvoker(new Uri("https://localhost/"));
        var request = new HttpRequestMessage(HttpMethod.Post, "https://localhost/api/notes");

        await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal("include", Credentials(request));
        Assert.Equal("the-token", request.Headers.GetValues("X-CSRF-TOKEN").Single());
    }

    [Fact]
    public async Task A_write_to_a_different_origin_carries_neither()
    {
        using var invoker = CreateInvoker(new Uri("https://localhost/"));
        var request = new HttpRequestMessage(HttpMethod.Post, "https://evil.example/api/notes");

        await invoker.SendAsync(request, CancellationToken.None);

        Assert.Null(Credentials(request));
        Assert.False(request.Headers.Contains("X-CSRF-TOKEN"));
    }

    [Fact]
    public async Task A_read_from_the_api_origin_carries_credentials_but_no_token()
    {
        using var invoker = CreateInvoker(new Uri("https://localhost/"));
        var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/notes");

        await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal("include", Credentials(request));
        Assert.False(request.Headers.Contains("X-CSRF-TOKEN"));
    }

    [Fact]
    public async Task A_relative_request_uri_is_treated_as_same_origin()
    {
        using var invoker = CreateInvoker(new Uri("https://localhost/"));
        var request = new HttpRequestMessage(HttpMethod.Post, "api/notes");

        await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal("include", Credentials(request));
        Assert.Equal("the-token", request.Headers.GetValues("X-CSRF-TOKEN").Single());
    }

    [Fact]
    public async Task A_different_port_on_the_same_host_is_a_different_origin()
    {
        using var invoker = CreateInvoker(new Uri("https://localhost:5000/"));
        var request = new HttpRequestMessage(HttpMethod.Post, "https://localhost:5001/api/notes");

        await invoker.SendAsync(request, CancellationToken.None);

        Assert.Null(Credentials(request));
        Assert.False(request.Headers.Contains("X-CSRF-TOKEN"));
    }
}
