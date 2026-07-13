using NacosSyncTool.Windows.Modules.RabbitMq.Models;
using Xunit;

namespace NacosSyncTool.Windows.Tests.RabbitMq;

public sealed class RabbitMqModelTests
{
    [Fact]
    public void BindingIdentityIgnoresArgumentsButIncludesRoutingKey()
    {
        var first = new RabbitMqBinding(
            "events",
            "orders",
            RabbitMqBindingDestination.Queue,
            "created",
            new Dictionary<string, object?> { ["x-match"] = "all" });
        var second = new RabbitMqBinding(
            "events",
            "orders",
            RabbitMqBindingDestination.Queue,
            "created",
            new Dictionary<string, object?>());
        var different = second with { RoutingKey = "cancelled" };

        Assert.Equal(first.Identity, second.Identity);
        Assert.NotEqual(first.Identity, different.Identity);
    }

    [Fact]
    public void QueueTotalContainsReadyAndUnacknowledgedMessages()
    {
        var queue = new RabbitMqQueue(
            "orders",
            true,
            false,
            false,
            new Dictionary<string, object?>(),
            12,
            3,
            1);

        Assert.Equal(15, queue.MessagesTotal);
    }
}
