using System.Net;

namespace NacosSyncTool.Windows.Tests.RabbitMq;

internal sealed record RecordedHttpRequest(HttpMethod Method, Uri RequestUri, string Body);

internal sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<RecordedHttpRequest> Requests { get; } = new();
    public RecordedHttpRequest? LastRequest => Requests.LastOrDefault();

    public RecordingHttpMessageHandler(HttpStatusCode statusCode, string body)
    {
        Enqueue(statusCode, body);
    }

    public void Enqueue(HttpStatusCode statusCode, string body)
    {
        _responses.Enqueue(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body)
        });
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content == null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RecordedHttpRequest(
            request.Method,
            request.RequestUri ?? throw new InvalidOperationException("Request URI was missing."),
            body));

        if (_responses.Count == 0)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };
        }

        return _responses.Dequeue()(request);
    }
}
