using System.Net;
using System.Net.Http;
using NacosSyncTool.Windows.Modules.RabbitMq.Models;

namespace NacosSyncTool.Windows.Modules.RabbitMq.Services;

public sealed class RabbitMqSyncExecutor
{
    private readonly IRabbitMqManagementClient _targetClient;

    public RabbitMqSyncExecutor(IRabbitMqManagementClient targetClient)
    {
        _targetClient = targetClient;
    }

    public async Task<RabbitMqExecutionSummary> ExecuteAsync(
        RabbitMqSyncPlan plan,
        IProgress<(int Completed, int Total, string Current)>? progress,
        CancellationToken cancellationToken)
    {
        var results = new List<RabbitMqExecutionItem>();
        var failedExchanges = new HashSet<string>(StringComparer.Ordinal);
        var failedQueues = new HashSet<string>(StringComparer.Ordinal);
        var virtualHostFailed = false;
        var total = plan.Items.Count;

        for (var index = 0; index < plan.Items.Count; index++)
        {
            var item = plan.Items[index];
            if (cancellationToken.IsCancellationRequested)
            {
                results.Add(new RabbitMqExecutionItem(
                    item.Type,
                    item.Identity,
                    RabbitMqExecutionStatus.Cancelled,
                    "用户已取消后续同步"));
                break;
            }

            progress?.Report((index, total, item.Identity));

            if (virtualHostFailed && item.Type != RabbitMqResourceType.VirtualHost)
            {
                results.Add(Result(
                    item,
                    RabbitMqExecutionStatus.Missing,
                    "Virtual Host 创建失败"));
                continue;
            }

            if (item.Status == RabbitMqPlanStatus.ExistingSkip)
            {
                results.Add(Result(item, RabbitMqExecutionStatus.Skipped, item.Reason));
                continue;
            }

            if (item.Status != RabbitMqPlanStatus.PendingCreate)
            {
                results.Add(Result(
                    item,
                    item.Status == RabbitMqPlanStatus.MissingDependency
                        ? RabbitMqExecutionStatus.Missing
                        : RabbitMqExecutionStatus.Failed,
                    item.Reason));
                continue;
            }

            if (item.Resource is RabbitMqBinding binding &&
                (failedExchanges.Contains(binding.Source) ||
                 (binding.DestinationType == RabbitMqBindingDestination.Exchange
                     ? failedExchanges.Contains(binding.Destination)
                     : failedQueues.Contains(binding.Destination))))
            {
                results.Add(Result(
                    item,
                    RabbitMqExecutionStatus.Missing,
                    "Binding 依赖资源创建失败"));
                continue;
            }

            try
            {
                if (await ExistsAsync(plan.VirtualHost, item, cancellationToken))
                {
                    results.Add(Result(item, RabbitMqExecutionStatus.Skipped, "执行前复查发现目标资源已存在"));
                    continue;
                }

                await ExecuteWithRetryAsync(
                    () => CreateAsync(plan.VirtualHost, item, cancellationToken),
                    cancellationToken);
                results.Add(Result(item, RabbitMqExecutionStatus.Created, "创建成功"));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                results.Add(Result(item, RabbitMqExecutionStatus.Cancelled, "用户已取消"));
                break;
            }
            catch (Exception ex)
            {
                if (item.Type == RabbitMqResourceType.Exchange)
                    failedExchanges.Add(item.Identity);
                if (item.Type == RabbitMqResourceType.Queue)
                    failedQueues.Add(item.Identity);
                if (item.Type == RabbitMqResourceType.VirtualHost)
                    virtualHostFailed = true;

                results.Add(Result(item, RabbitMqExecutionStatus.Failed, ex.Message));
                if (ex is RabbitMqApiException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden })
                    break;
            }
        }

        progress?.Report((results.Count, total, string.Empty));
        return new RabbitMqExecutionSummary(results);
    }

    private async Task<bool> ExistsAsync(
        string virtualHost,
        RabbitMqSyncPlanItem item,
        CancellationToken cancellationToken)
    {
        return item.Type switch
        {
            RabbitMqResourceType.VirtualHost => (await _targetClient.GetVirtualHostsAsync(cancellationToken))
                .Any(value => string.Equals(value.Name, item.Identity, StringComparison.Ordinal)),
            RabbitMqResourceType.Exchange => (await _targetClient.GetExchangesAsync(virtualHost, cancellationToken))
                .Any(value => string.Equals(value.Name, item.Identity, StringComparison.Ordinal)),
            RabbitMqResourceType.Policy => (await _targetClient.GetPoliciesAsync(virtualHost, cancellationToken))
                .Any(value => string.Equals(value.Name, item.Identity, StringComparison.Ordinal)),
            RabbitMqResourceType.Queue => (await _targetClient.GetQueuesAsync(virtualHost, cancellationToken))
                .Any(value => string.Equals(value.Name, item.Identity, StringComparison.Ordinal)),
            RabbitMqResourceType.Binding => (await _targetClient.GetBindingsAsync(virtualHost, cancellationToken))
                .Any(value => string.Equals(value.Identity, item.Identity, StringComparison.Ordinal)),
            _ => false
        };
    }

    private Task CreateAsync(
        string virtualHost,
        RabbitMqSyncPlanItem item,
        CancellationToken cancellationToken)
    {
        return item.Resource switch
        {
            RabbitMqVirtualHost value => _targetClient.CreateVirtualHostAsync(value, cancellationToken),
            RabbitMqExchange value => _targetClient.CreateExchangeAsync(virtualHost, value, cancellationToken),
            RabbitMqPolicy value => _targetClient.CreatePolicyAsync(virtualHost, value, cancellationToken),
            RabbitMqQueue value => _targetClient.CreateQueueAsync(virtualHost, value, cancellationToken),
            RabbitMqBinding value => _targetClient.CreateBindingAsync(virtualHost, value, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported RabbitMQ resource: {item.Resource.GetType().Name}")
        };
    }

    private static async Task ExecuteWithRetryAsync(
        Func<Task> action,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await action();
                return;
            }
            catch (Exception ex) when (attempt < 3 && IsTransient(ex, cancellationToken))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), cancellationToken);
            }
        }
    }

    private static bool IsTransient(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException ||
        exception is TaskCanceledException && !cancellationToken.IsCancellationRequested ||
        exception is RabbitMqApiException apiException && (int)apiException.StatusCode >= 500;

    private static RabbitMqExecutionItem Result(
        RabbitMqSyncPlanItem item,
        RabbitMqExecutionStatus status,
        string message) =>
        new(item.Type, item.Identity, status, message);
}
