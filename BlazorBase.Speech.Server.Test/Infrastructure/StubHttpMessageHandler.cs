namespace BlazorBase.Speech.Server.Test.Infrastructure;

/// <summary>Answers every request through a delegate and remembers the requests it saw.</summary>
public sealed class StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    #region Injects
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> Respond = respond;
    #endregion

    public List<HttpRequestMessage> Requests { get; } = [];

    public List<string> RequestBodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        RequestBodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
        return await Respond(request);
    }
}
