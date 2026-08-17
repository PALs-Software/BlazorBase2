namespace BlazorBase.CRUD.Test.Infrastructure;

/// <summary>
/// Test double for <see cref="HttpMessageHandler"/> that records every outgoing request
/// (including its body) and returns a caller-supplied response. Used to verify the REST
/// contract of <see cref="BlazorBase.CRUD.DataProviders.HttpBaseDataProvider{TModel}"/>
/// without a live server.
/// </summary>
public sealed class RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> Responder = responder;

    public List<HttpRequestMessage> Requests { get; } = [];

    public HttpRequestMessage LastRequest => Requests[^1];

    public string? LastRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);

        if (request.Content is not null)
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);

        return Responder(request);
    }
}
