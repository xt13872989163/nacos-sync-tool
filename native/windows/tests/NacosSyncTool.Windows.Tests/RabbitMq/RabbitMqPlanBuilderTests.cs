using NacosSyncTool.Windows.Modules.RabbitMq.Models;
using NacosSyncTool.Windows.Modules.RabbitMq.Services;
using Xunit;

namespace NacosSyncTool.Windows.Tests.RabbitMq;

public sealed class RabbitMqPlanBuilderTests
{
    [Fact]
    public void ExistingNamesSkipWithoutComparingConfiguration()
    {
        var source = Snapshot(
            exchanges: [new RabbitMqExchange("orders", "direct", true, false, false, [])],
            queues: [new RabbitMqQueue("orders", true, false, false, [], 0, 0, 0)]);
        var target = Snapshot(
            exchanges: [new RabbitMqExchange("orders", "fanout", false, true, true, [])],
            queues: [new RabbitMqQueue("orders", false, true, false, [], 0, 0, 0)]);

        var plan = new RabbitMqPlanBuilder().Build(
            source,
            target,
            Set("orders"),
            Set(),
            Set("orders"),
            Set());

        Assert.All(
            plan.Items.Where(item => item.Type is RabbitMqResourceType.Exchange or RabbitMqResourceType.Queue),
            item => Assert.Equal(RabbitMqPlanStatus.ExistingSkip, item.Status));
    }

    [Fact]
    public void BindingRequiresSelectedOrExistingDependencies()
    {
        var binding = new RabbitMqBinding(
            "events",
            "orders",
            RabbitMqBindingDestination.Queue,
            "created",
            []);
        var source = Snapshot(
            exchanges: [new RabbitMqExchange("events", "topic", true, false, false, [])],
            queues: [new RabbitMqQueue("orders", true, false, false, [], 0, 0, 0)],
            bindings: [binding]);

        var plan = new RabbitMqPlanBuilder().Build(
            source,
            Snapshot(),
            Set(),
            Set(),
            Set(),
            Set(binding.Identity));

        var item = Assert.Single(plan.Items.Where(candidate => candidate.Type == RabbitMqResourceType.Binding));
        Assert.Equal(RabbitMqPlanStatus.MissingDependency, item.Status);
    }

    [Fact]
    public void PlanUsesRequiredDependencyOrder()
    {
        var binding = new RabbitMqBinding("events", "orders", RabbitMqBindingDestination.Queue, "", []);
        var source = Snapshot(
            exchanges: [new RabbitMqExchange("events", "direct", true, false, false, [])],
            policies: [new RabbitMqPolicy("ttl", ".*", [], 0, "queues")],
            queues: [new RabbitMqQueue("orders", true, false, false, [], 0, 0, 0)],
            bindings: [binding]);

        var plan = new RabbitMqPlanBuilder().Build(
            source,
            null,
            Set("events"),
            Set("ttl"),
            Set("orders"),
            Set(binding.Identity));

        Assert.Equal(
            [RabbitMqResourceType.VirtualHost, RabbitMqResourceType.Exchange, RabbitMqResourceType.Policy, RabbitMqResourceType.Queue, RabbitMqResourceType.Binding],
            plan.Items.Select(item => item.Type));
    }

    private static HashSet<string> Set(params string[] values) =>
        values.ToHashSet(StringComparer.Ordinal);

    private static RabbitMqTopologySnapshot Snapshot(
        IReadOnlyList<RabbitMqExchange>? exchanges = null,
        IReadOnlyList<RabbitMqPolicy>? policies = null,
        IReadOnlyList<RabbitMqQueue>? queues = null,
        IReadOnlyList<RabbitMqBinding>? bindings = null) =>
        new(
            new RabbitMqVirtualHost("/", null, [], null),
            exchanges ?? [],
            policies ?? [],
            queues ?? [],
            bindings ?? []);
}
