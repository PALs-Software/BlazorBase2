using System.Net;
using System.Text;
using BlazorBase.Files.Services;
using Xunit;

namespace BlazorBase.Files.Server.Test.Services;

public class HttpFileUploadClientTests
{
    [Theory]
    [InlineData("plain-token", "text/plain", "plain-token")]
    [InlineData("\"json-token\"", "application/json", "json-token")]
    [InlineData("  padded-token\r\n", "text/plain", "padded-token")]
    public async Task GetAccessTokenAsync_ReadsPlainTextAndJsonResponses(string body, string mediaType, string expected)
    {
        using var httpClient = new HttpClient(new StubHandler(body, mediaType))
        {
            BaseAddress = new Uri("https://localhost/api/files/")
        };
        var client = new HttpFileUploadClient(httpClient);

        var token = await client.GetAccessTokenAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(expected, token);
    }

    private sealed class StubHandler(string body, string mediaType) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, mediaType)
            };
            return Task.FromResult(response);
        }
    }
}
