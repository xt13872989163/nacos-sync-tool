using System.Net;
using System.Net.Http;
using NacosSyncTool.Windows.Modules.RabbitMq.Models;
using NacosSyncTool.Windows.Modules.RabbitMq.Services;
using Xunit;

namespace NacosSyncTool.Windows.Tests.RabbitMq;

public sealed class RabbitMqManagementClientSecurityTests
{
    [Fact]
    public async Task EncodesDefaultVhostAndQueueNameForPurge()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.NoContent, string.Empty);
        using var client = new RabbitMqManagementClient(
            Settings(),
            RabbitMqClientRole.QueuePurge,
            handler);

        await client.PurgeQueueAsync("/", "orders/a", CancellationToken.None);

        Assert.Equal(HttpMethod.Delete, handler.LastRequest!.Method);
        Assert.EndsWith(
            "api/queues/%2F/orders%2Fa/contents",
            handler.LastRequest.RequestUri.AbsoluteUri);
    }

    [Fact]
    public async Task PreservesReverseProxyBasePath()
    {
        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.OK,
            "{\"rabbitmq_version\":\"4.1\",\"cluster_name\":\"cluster-a\"}");
        using var client = new RabbitMqManagementClient(
            Settings("https://gateway.example/rabbit/"),
            RabbitMqClientRole.SourceReadOnly,
            handler);

        await client.TestConnectionAsync(CancellationToken.None);

        Assert.Equal("/rabbit/api/overview", handler.LastRequest!.RequestUri.AbsolutePath);
    }

    [Fact]
    public async Task SourceRoleCannotCreateOrPurge()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, "{}");
        using var client = new RabbitMqManagementClient(
            Settings(),
            RabbitMqClientRole.SourceReadOnly,
            handler);
        var exchange = new RabbitMqExchange("orders", "direct", true, false, false, []);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.CreateExchangeAsync("/", exchange, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.PurgeQueueAsync("/", "orders", CancellationToken.None));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void InterfaceExposesNoResourceDeletionMethods()
    {
        var names = typeof(IRabbitMqManagementClient).GetMethods().Select(method => method.Name).ToList();

        Assert.DoesNotContain("DeleteVirtualHostAsync", names);
        Assert.DoesNotContain("DeleteQueueAsync", names);
        Assert.DoesNotContain("DeleteExchangeAsync", names);
        Assert.DoesNotContain("DeleteBindingAsync", names);
        Assert.DoesNotContain("DeletePolicyAsync", names);
        Assert.DoesNotContain("DeleteAsync", names);
    }

    private static RabbitMqConnectionSettings Settings(
        string address = "http://localhost:15672/") =>
        new(address, "guest", "guest", TimeSpan.FromSeconds(5));
}
