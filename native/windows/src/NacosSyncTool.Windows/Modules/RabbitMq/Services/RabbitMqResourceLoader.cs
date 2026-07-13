using NacosSyncTool.Windows.Modules.RabbitMq.Models;

namespace NacosSyncTool.Windows.Modules.RabbitMq.Services;

public sealed class RabbitMqResourceLoader
{
    private readonly IRabbitMqManagementClient _client;

    public RabbitMqResourceLoader(IRabbitMqManagementClient client)
    {
        _client = client;
    }

    public async Task<RabbitMqTopologySnapshot> LoadSyncSnapshotAsync(
        string virtualHost,
        CancellationToken cancellationToken)
    {
        var virtualHosts = await _client.GetVirtualHostsAsync(cancellationToken);
        var selectedVirtualHost = virtualHosts.FirstOrDefault(item =>
            string.Equals(item.Name, virtualHost, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Virtual Host does not exist: {virtualHost}");

        var exchangesTask = _client.GetExchangesAsync(virtualHost, cancellationToken);
        var policiesTask = _client.GetPoliciesAsync(virtualHost, cancellationToken);
        var queuesTask = _client.GetQueuesAsync(virtualHost, cancellationToken);
        var bindingsTask = _client.GetBindingsAsync(virtualHost, cancellationToken);
        await Task.WhenAll(exchangesTask, policiesTask, queuesTask, bindingsTask);

        var exchanges = exchangesTask.Result
            .Where(IsSynchronizableExchange)
            .ToList();
        var queues = queuesTask.Result
            .Where(IsSynchronizableQueue)
            .ToList();
        var exchangeNames = exchanges.Select(item => item.Name).ToHashSet(StringComparer.Ordinal);
        var queueNames = queues.Select(item => item.Name).ToHashSet(StringComparer.Ordinal);
        var bindings = bindingsTask.Result
            .Where(binding => exchangeNames.Contains(binding.Source))
            .Where(binding => binding.DestinationType == RabbitMqBindingDestination.Exchange
                ? exchangeNames.Contains(binding.Destination)
                : queueNames.Contains(binding.Destination))
            .ToList();

        return new RabbitMqTopologySnapshot(
            selectedVirtualHost,
            exchanges,
            policiesTask.Result.ToList(),
            queues,
            bindings);
    }

    public async Task<IReadOnlyList<RabbitMqQueue>> LoadPurgeQueuesAsync(
        string virtualHost,
        CancellationToken cancellationToken)
    {
        return await _client.GetQueuesAsync(virtualHost, cancellationToken);
    }

    public static bool IsSynchronizableExchange(RabbitMqExchange exchange) =>
        !string.IsNullOrEmpty(exchange.Name) &&
        !exchange.Name.StartsWith("amq.", StringComparison.Ordinal);

    public static bool IsSynchronizableQueue(RabbitMqQueue queue) =>
        !queue.Exclusive &&
        !queue.Name.StartsWith("amq.gen-", StringComparison.Ordinal);
}
