using NacosSyncTool.Windows.Modules.RabbitMq.Models;

namespace NacosSyncTool.Windows.Modules.RabbitMq.Services;

public sealed class RabbitMqPlanBuilder
{
    public RabbitMqSyncPlan Build(
        RabbitMqTopologySnapshot source,
        RabbitMqTopologySnapshot? target,
        IReadOnlySet<string> selectedExchangeNames,
        IReadOnlySet<string> selectedPolicyNames,
        IReadOnlySet<string> selectedQueueNames,
        IReadOnlySet<string> selectedBindingIdentities)
    {
        var items = new List<RabbitMqSyncPlanItem>();
        var targetExchangeNames = target?.Exchanges
            .Select(item => item.Name)
            .ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);
        var targetPolicyNames = target?.Policies
            .Select(item => item.Name)
            .ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);
        var targetQueueNames = target?.Queues
            .Select(item => item.Name)
            .ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);
        var targetBindingIdentities = target?.Bindings
            .Select(item => item.Identity)
            .ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);

        items.Add(new RabbitMqSyncPlanItem(
            RabbitMqResourceType.VirtualHost,
            source.VirtualHost.Name,
            source.VirtualHost,
            target == null ? RabbitMqPlanStatus.PendingCreate : RabbitMqPlanStatus.ExistingSkip,
            target == null ? "目标 Virtual Host 不存在，将创建" : "目标 Virtual Host 已存在，跳过"));

        foreach (var exchange in source.Exchanges.Where(item => selectedExchangeNames.Contains(item.Name)))
        {
            items.Add(CreateNamedItem(
                RabbitMqResourceType.Exchange,
                exchange.Name,
                exchange,
                targetExchangeNames.Contains(exchange.Name)));
        }

        foreach (var policy in source.Policies.Where(item => selectedPolicyNames.Contains(item.Name)))
        {
            items.Add(CreateNamedItem(
                RabbitMqResourceType.Policy,
                policy.Name,
                policy,
                targetPolicyNames.Contains(policy.Name)));
        }

        foreach (var queue in source.Queues.Where(item => selectedQueueNames.Contains(item.Name)))
        {
            items.Add(CreateNamedItem(
                RabbitMqResourceType.Queue,
                queue.Name,
                queue,
                targetQueueNames.Contains(queue.Name)));
        }

        var availableExchanges = new HashSet<string>(targetExchangeNames, StringComparer.Ordinal);
        availableExchanges.UnionWith(selectedExchangeNames);
        var availableQueues = new HashSet<string>(targetQueueNames, StringComparer.Ordinal);
        availableQueues.UnionWith(selectedQueueNames);

        foreach (var binding in source.Bindings.Where(item => selectedBindingIdentities.Contains(item.Identity)))
        {
            var sourceExists = availableExchanges.Contains(binding.Source);
            var destinationExists = binding.DestinationType == RabbitMqBindingDestination.Exchange
                ? availableExchanges.Contains(binding.Destination)
                : availableQueues.Contains(binding.Destination);
            var exists = targetBindingIdentities.Contains(binding.Identity);
            var status = exists
                ? RabbitMqPlanStatus.ExistingSkip
                : sourceExists && destinationExists
                    ? RabbitMqPlanStatus.PendingCreate
                    : RabbitMqPlanStatus.MissingDependency;
            var reason = status switch
            {
                RabbitMqPlanStatus.ExistingSkip => "目标 Binding 已存在，跳过",
                RabbitMqPlanStatus.PendingCreate => "目标 Binding 不存在，将创建",
                _ => "Binding 的来源或目标资源未选择且目标端不存在"
            };
            items.Add(new RabbitMqSyncPlanItem(
                RabbitMqResourceType.Binding,
                binding.Identity,
                binding,
                status,
                reason));
        }

        return new RabbitMqSyncPlan(source.VirtualHost.Name, DateTimeOffset.UtcNow, items);
    }

    private static RabbitMqSyncPlanItem CreateNamedItem(
        RabbitMqResourceType type,
        string identity,
        object resource,
        bool exists)
    {
        return new RabbitMqSyncPlanItem(
            type,
            identity,
            resource,
            exists ? RabbitMqPlanStatus.ExistingSkip : RabbitMqPlanStatus.PendingCreate,
            exists ? "目标同名资源已存在，跳过" : "目标同名资源不存在，将创建");
    }
}
