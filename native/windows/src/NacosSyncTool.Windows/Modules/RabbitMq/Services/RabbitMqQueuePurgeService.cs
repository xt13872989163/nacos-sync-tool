using System.Net;
using NacosSyncTool.Windows.Modules.RabbitMq.Models;

namespace NacosSyncTool.Windows.Modules.RabbitMq.Services;

public sealed class RabbitMqQueuePurgeService
{
    private readonly IRabbitMqManagementClient _client;
    private readonly RabbitMqResourceLoader _loader;

    public RabbitMqQueuePurgeService(IRabbitMqManagementClient client)
    {
        _client = client;
        _loader = new RabbitMqResourceLoader(client);
    }

    public async Task<RabbitMqQueuePurgePreview> BuildPreviewAsync(
        string virtualHost,
        CancellationToken cancellationToken)
    {
        var queues = await _loader.LoadPurgeQueuesAsync(virtualHost, cancellationToken);
        return new RabbitMqQueuePurgePreview(
            virtualHost,
            queues.ToList(),
            queues.Sum(item => item.MessagesReady),
            queues.Sum(item => item.MessagesUnacknowledged));
    }

    public async Task<RabbitMqQueuePurgeSummary> ExecuteAsync(
        RabbitMqQueuePurgePreview preview,
        IProgress<(int Completed, int Total, string Current)>? progress,
        CancellationToken cancellationToken)
    {
        var results = new List<RabbitMqQueuePurgeItem>();
        var cancelled = false;

        for (var index = 0; index < preview.Queues.Count; index++)
        {
            var queue = preview.Queues[index];
            if (cancellationToken.IsCancellationRequested)
            {
                cancelled = true;
                break;
            }

            progress?.Report((index, preview.Queues.Count, queue.Name));
            try
            {
                await ExecuteWithRetryAsync(
                    () => _client.PurgeQueueAsync(preview.VirtualHost, queue.Name, cancellationToken),
                    cancellationToken);
                results.Add(new RabbitMqQueuePurgeItem(
                    queue.Name,
                    queue.MessagesReady,
                    RabbitMqExecutionStatus.Succeeded,
                    "Ready 消息已清空"));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                cancelled = true;
                break;
            }
            catch (RabbitMqApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                results.Add(new RabbitMqQueuePurgeItem(
                    queue.Name,
                    queue.MessagesReady,
                    RabbitMqExecutionStatus.Missing,
                    "Queue 已不存在"));
            }
            catch (Exception ex)
            {
                results.Add(new RabbitMqQueuePurgeItem(
                    queue.Name,
                    queue.MessagesReady,
                    RabbitMqExecutionStatus.Failed,
                    ex.Message));
                if (ex is RabbitMqApiException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden })
                    break;
            }
        }

        var refreshed = await TryRefreshAsync(preview.VirtualHost);
        progress?.Report((results.Count, preview.Queues.Count, string.Empty));
        return new RabbitMqQueuePurgeSummary(
            preview.VirtualHost,
            results,
            refreshed.Sum(item => item.MessagesReady),
            refreshed.Sum(item => item.MessagesUnacknowledged),
            cancelled);
    }

    private async Task<IReadOnlyList<RabbitMqQueue>> TryRefreshAsync(string virtualHost)
    {
        try
        {
            return await _loader.LoadPurgeQueuesAsync(virtualHost, CancellationToken.None);
        }
        catch
        {
            return Array.Empty<RabbitMqQueue>();
        }
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
}
