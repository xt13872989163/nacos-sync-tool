using System.Net;
using NacosSyncTool.Windows.Modules.RabbitMq.Models;
using NacosSyncTool.Windows.Modules.RabbitMq.Services;
using Xunit;

namespace NacosSyncTool.Windows.Tests.RabbitMq;

public sealed class RabbitMqSyncExecutorTests
{
    [Fact]
    public async Task CreatesResourcesInDependencyOrderWithoutPurging()
    {
        var client = new FakeRabbitMqManagementClient();
        var plan = FullPlan();

        var result = await new RabbitMqSyncExecutor(client)
            .ExecuteAsync(plan, null, CancellationToken.None);

        Assert.Equal(5, result.CreatedCount);
        Assert.Equal(
            ["CreateVirtualHost:/", "CreateExchange:/:events", "CreatePolicy:/:ttl", "CreateQueue:/:orders"],
            client.Calls.Where(call => call.StartsWith("Create", StringComparison.Ordinal)).Take(4));
        Assert.Contains(client.Calls, call => call.StartsWith("CreateBinding:/", StringComparison.Ordinal));
        Assert.DoesNotContain(client.Calls, call => call.StartsWith("PurgeQueue", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RetriesTransientFailuresAtMostTwice()
    {
        var attempts = 0;
        var client = new FakeRabbitMqManagementClient
        {
            VirtualHosts = [new RabbitMqVirtualHost("/", null, [], null)],
            FailureForCall = call => call.StartsWith("CreateExchange", StringComparison.Ordinal) && ++attempts < 3
                ? new RabbitMqApiException(HttpStatusCode.ServiceUnavailable, "api/exchanges", "busy")
                : null
        };
        var exchange = new RabbitMqExchange("events", "direct", true, false, false, []);
        var plan = new RabbitMqSyncPlan(
            "/",
            DateTimeOffset.UtcNow,
            [new RabbitMqSyncPlanItem(RabbitMqResourceType.Exchange, "events", exchange, RabbitMqPlanStatus.PendingCreate, "create")]);

        var result = await new RabbitMqSyncExecutor(client)
            .ExecuteAsync(plan, null, CancellationToken.None);

        Assert.Equal(3, attempts);
        Assert.Equal(RabbitMqExecutionStatus.Created, Assert.Single(result.Items).Status);
    }

    private static RabbitMqSyncPlan FullPlan()
    {
        var virtualHost = new RabbitMqVirtualHost("/", null, [], null);
        var exchange = new RabbitMqExchange("events", "direct", true, false, false, []);
        var policy = new RabbitMqPolicy("ttl", ".*", [], 0, "queues");
        var queue = new RabbitMqQueue("orders", true, false, false, [], 0, 0, 0);
        var binding = new RabbitMqBinding("events", "orders", RabbitMqBindingDestination.Queue, "", []);
        return new RabbitMqSyncPlan(
            "/",
            DateTimeOffset.UtcNow,
            [
                Item(RabbitMqResourceType.VirtualHost, "/", virtualHost),
                Item(RabbitMqResourceType.Exchange, "events", exchange),
                Item(RabbitMqResourceType.Policy, "ttl", policy),
                Item(RabbitMqResourceType.Queue, "orders", queue),
                Item(RabbitMqResourceType.Binding, binding.Identity, binding)
            ]);
    }

    private static RabbitMqSyncPlanItem Item(RabbitMqResourceType type, string identity, object value) =>
        new(type, identity, value, RabbitMqPlanStatus.PendingCreate, "create");
}
