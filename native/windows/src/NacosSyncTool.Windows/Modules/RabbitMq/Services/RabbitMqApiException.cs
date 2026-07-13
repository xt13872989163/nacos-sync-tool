using System.Net;

namespace NacosSyncTool.Windows.Modules.RabbitMq.Services;

public sealed class RabbitMqApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string RequestPath { get; }

    public RabbitMqApiException(
        HttpStatusCode statusCode,
        string requestPath,
        string responseSummary)
        : base($"RabbitMQ API {statusCode} at {requestPath}: {responseSummary}")
    {
        StatusCode = statusCode;
        RequestPath = requestPath;
    }
}
