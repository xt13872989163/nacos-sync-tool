using NacosSyncTool.Windows.Modules.RabbitMq.Models;
using NacosSyncTool.Windows.Modules.RabbitMq.Services;
using Xunit;

namespace NacosSyncTool.Windows.Tests.RabbitMq;

public sealed class RabbitMqResourceLoaderTests
{
    [Fact]
    public async Task SyncSnapshotFiltersBuiltInAndConnectionScopedResources()
    {
        var client = new FakeRabbitMqManagementClient
        {
            VirtualHosts = [new RabbitMqVirtualHost("/", null, [], null)],
            Exchanges = [Exchange(""), Exchange("amq.direct"), Exchange("orders")],
            Queues = [Queue("amq.gen-temp"), Queue("exclusive", true), Queue("orders")],
            Bindings =
            [
                new RabbitMqBinding("orders", "orders", RabbitMqBindingDestination.Queue, "", []),
                new RabbitMqBinding("amq.direct", "orders", RabbitMqBindingDestination.Queue, "", [])
            ]
        };

        var snapshot = await new RabbitMqResourceLoader(client)
            .LoadSyncSnapshotAsync("/", CancellationToken.None);

        Assert.Equal(["orders"], snapshot.Exchanges.Select(item => item.Name));
        Assert.Equal(["orders"], snapshot.Queues.Select(item => item.Name));
        Assert.Single(snapshot.Bindings);
    }

    [Fact]
    public async Task PurgeListKeepsGeneratedAndExclusiveQueues()
    {
        var client = new FakeRabbitMqManagementClient
        {
            Queues = [Queue("amq.gen-temp"), Queue("exclusive", true)]
        };

        var queues = await new RabbitMqResourceLoader(client)
            .LoadPurgeQueuesAsync("/", CancellationToken.None);

        Assert.Equal(2, queues.Count);
    }

    private static RabbitMqExchange Exchange(string name) =>
        new(name, "direct", true, false, false, []);

    private static RabbitMqQueue Queue(string name, bool exclusive = false) =>
        new(name, true, false, exclusive, [], 0, 0, 0);
}
