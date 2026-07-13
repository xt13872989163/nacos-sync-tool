using System.Net;
using NacosSyncTool.Windows.Modules.RabbitMq.Models;
using NacosSyncTool.Windows.Modules.RabbitMq.Services;
using Xunit;

namespace NacosSyncTool.Windows.Tests.RabbitMq;

public sealed class RabbitMqManagementClientReadTests
{
    [Fact]
    public async Task ReadsQueueMessageAndConsumerCounts()
    {
        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.OK,
            "[{\"name\":\"orders\",\"durable\":true,\"auto_delete\":false,\"exclusive\":false,\"arguments\":{\"x-queue-type\":\"quorum\"},\"messages_ready\":12,\"messages_unacknowledged\":3,\"consumers\":2}]");
        using var client = new RabbitMqManagementClient(
            Settings(),
            RabbitMqClientRole.SourceReadOnly,
            handler);

        var queues = await client.GetQueuesAsync("/", CancellationToken.None);

        var queue = Assert.Single(queues);
        Assert.Equal("orders", queue.Name);
        Assert.Equal(12, queue.MessagesReady);
        Assert.Equal(3, queue.MessagesUnacknowledged);
        Assert.Equal(2, queue.Consumers);
        Assert.EndsWith("api/queues/%2F", handler.LastRequest!.RequestUri.AbsoluteUri);
    }

    [Fact]
    public async Task ReadsBindingDestinationTypeAndIdentity()
    {
        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.OK,
            "[{\"source\":\"events\",\"destination\":\"orders\",\"destination_type\":\"queue\",\"routing_key\":\"created\",\"arguments\":{}}]");
        using var client = new RabbitMqManagementClient(
            Settings(),
            RabbitMqClientRole.SourceReadOnly,
            handler);

        var bindings = await client.GetBindingsAsync("/dev", CancellationToken.None);

        var binding = Assert.Single(bindings);
        Assert.Equal(RabbitMqBindingDestination.Queue, binding.DestinationType);
        Assert.Contains("created", binding.Identity);
    }

    private static RabbitMqConnectionSettings Settings() =>
        new("http://localhost:15672/", "guest", "guest", TimeSpan.FromSeconds(5));
}
