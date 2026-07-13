using System.Net;
using System.Net.Http;
using System.Text.Json;
using NacosSyncTool.Windows.Modules.RabbitMq.Models;
using NacosSyncTool.Windows.Modules.RabbitMq.Services;
using Xunit;

namespace NacosSyncTool.Windows.Tests.RabbitMq;

public sealed class RabbitMqManagementClientWriteTests
{
    [Fact]
    public async Task CreatesQueueWithoutExclusiveAndPreservesArguments()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.Created, string.Empty);
        using var client = new RabbitMqManagementClient(
            Settings(),
            RabbitMqClientRole.TargetTopology,
            handler);
        var queue = new RabbitMqQueue(
            "orders",
            true,
            false,
            true,
            new Dictionary<string, object?> { ["x-queue-type"] = "quorum" },
            0,
            0,
            0);

        await client.CreateQueueAsync("/", queue, CancellationToken.None);

        using var body = JsonDocument.Parse(handler.LastRequest!.Body);
        Assert.False(body.RootElement.TryGetProperty("exclusive", out _));
        Assert.Equal(
            "quorum",
            body.RootElement.GetProperty("arguments").GetProperty("x-queue-type").GetString());
    }

    [Fact]
    public async Task CreatesBindingWithExplicitDestinationType()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.Created, string.Empty);
        using var client = new RabbitMqManagementClient(
            Settings(),
            RabbitMqClientRole.TargetTopology,
            handler);
        var binding = new RabbitMqBinding(
            "events",
            "orders",
            RabbitMqBindingDestination.Queue,
            "created",
            []);

        await client.CreateBindingAsync("/dev", binding, CancellationToken.None);

        Assert.Contains(
            "api/bindings/%2Fdev/e/events/q/orders",
            handler.LastRequest!.RequestUri.AbsoluteUri);
    }

    private static RabbitMqConnectionSettings Settings() =>
        new("http://localhost:15672/", "guest", "guest", TimeSpan.FromSeconds(5));
}
