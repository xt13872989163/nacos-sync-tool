using NacosSyncTool.Windows.Modules.RabbitMq.Models;

namespace NacosSyncTool.Windows.Modules.RabbitMq.Services;

public interface IRabbitMqManagementClient
{
    Task<RabbitMqConnectionInfo> TestConnectionAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<RabbitMqVirtualHost>> GetVirtualHostsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<RabbitMqExchange>> GetExchangesAsync(string virtualHost, CancellationToken cancellationToken);
    Task<IReadOnlyList<RabbitMqQueue>> GetQueuesAsync(string virtualHost, CancellationToken cancellationToken);
    Task<IReadOnlyList<RabbitMqBinding>> GetBindingsAsync(string virtualHost, CancellationToken cancellationToken);
    Task<IReadOnlyList<RabbitMqPolicy>> GetPoliciesAsync(string virtualHost, CancellationToken cancellationToken);
    Task CreateVirtualHostAsync(RabbitMqVirtualHost virtualHost, CancellationToken cancellationToken);
    Task CreateExchangeAsync(string virtualHost, RabbitMqExchange exchange, CancellationToken cancellationToken);
    Task CreateQueueAsync(string virtualHost, RabbitMqQueue queue, CancellationToken cancellationToken);
    Task CreateBindingAsync(string virtualHost, RabbitMqBinding binding, CancellationToken cancellationToken);
    Task CreatePolicyAsync(string virtualHost, RabbitMqPolicy policy, CancellationToken cancellationToken);
    Task PurgeQueueAsync(string virtualHost, string queueName, CancellationToken cancellationToken);
}
