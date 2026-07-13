using NacosSyncTool.Windows.Modules.RabbitMq.Models;
using NacosSyncTool.Windows.Modules.RabbitMq.Services;

namespace NacosSyncTool.Windows.Tests.RabbitMq;

internal sealed class FakeRabbitMqManagementClient : IRabbitMqManagementClient
{
    public RabbitMqConnectionInfo ConnectionInfo { get; set; } = new("4.1.0", "test-cluster");
    public IReadOnlyList<RabbitMqVirtualHost> VirtualHosts { get; set; } = [];
    public IReadOnlyList<RabbitMqExchange> Exchanges { get; set; } = [];
    public IReadOnlyList<RabbitMqQueue> Queues { get; set; } = [];
    public Queue<IReadOnlyList<RabbitMqQueue>> QueueResponses { get; } = new();
    public IReadOnlyList<RabbitMqBinding> Bindings { get; set; } = [];
    public IReadOnlyList<RabbitMqPolicy> Policies { get; set; } = [];
    public List<string> Calls { get; } = [];
    public Func<string, Exception?>? FailureForCall { get; set; }

    public Task<RabbitMqConnectionInfo> TestConnectionAsync(CancellationToken cancellationToken) =>
        ReturnAsync("TestConnection", ConnectionInfo);

    public Task<IReadOnlyList<RabbitMqVirtualHost>> GetVirtualHostsAsync(CancellationToken cancellationToken) =>
        ReturnAsync("GetVirtualHosts", VirtualHosts);

    public Task<IReadOnlyList<RabbitMqExchange>> GetExchangesAsync(string virtualHost, CancellationToken cancellationToken) =>
        ReturnAsync($"GetExchanges:{virtualHost}", Exchanges);

    public Task<IReadOnlyList<RabbitMqQueue>> GetQueuesAsync(string virtualHost, CancellationToken cancellationToken) =>
        ReturnAsync(
            $"GetQueues:{virtualHost}",
            QueueResponses.Count > 0 ? QueueResponses.Dequeue() : Queues);

    public Task<IReadOnlyList<RabbitMqBinding>> GetBindingsAsync(string virtualHost, CancellationToken cancellationToken) =>
        ReturnAsync($"GetBindings:{virtualHost}", Bindings);

    public Task<IReadOnlyList<RabbitMqPolicy>> GetPoliciesAsync(string virtualHost, CancellationToken cancellationToken) =>
        ReturnAsync($"GetPolicies:{virtualHost}", Policies);

    public Task CreateVirtualHostAsync(RabbitMqVirtualHost virtualHost, CancellationToken cancellationToken) =>
        RecordAsync($"CreateVirtualHost:{virtualHost.Name}");

    public Task CreateExchangeAsync(string virtualHost, RabbitMqExchange exchange, CancellationToken cancellationToken) =>
        RecordAsync($"CreateExchange:{virtualHost}:{exchange.Name}");

    public Task CreateQueueAsync(string virtualHost, RabbitMqQueue queue, CancellationToken cancellationToken) =>
        RecordAsync($"CreateQueue:{virtualHost}:{queue.Name}");

    public Task CreateBindingAsync(string virtualHost, RabbitMqBinding binding, CancellationToken cancellationToken) =>
        RecordAsync($"CreateBinding:{virtualHost}:{binding.Identity}");

    public Task CreatePolicyAsync(string virtualHost, RabbitMqPolicy policy, CancellationToken cancellationToken) =>
        RecordAsync($"CreatePolicy:{virtualHost}:{policy.Name}");

    public Task PurgeQueueAsync(string virtualHost, string queueName, CancellationToken cancellationToken) =>
        RecordAsync($"PurgeQueue:{virtualHost}:{queueName}");

    private Task<T> ReturnAsync<T>(string call, T value)
    {
        Calls.Add(call);
        var exception = FailureForCall?.Invoke(call);
        return exception == null ? Task.FromResult(value) : Task.FromException<T>(exception);
    }

    private Task RecordAsync(string call)
    {
        Calls.Add(call);
        var exception = FailureForCall?.Invoke(call);
        return exception == null ? Task.CompletedTask : Task.FromException(exception);
    }
}
