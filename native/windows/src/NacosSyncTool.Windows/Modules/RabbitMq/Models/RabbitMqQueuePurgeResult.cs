namespace NacosSyncTool.Windows.Modules.RabbitMq.Models;

public sealed record RabbitMqQueuePurgePreview(
    string VirtualHost,
    IReadOnlyList<RabbitMqQueue> Queues,
    long ReadyTotal,
    long UnackedTotal);

public sealed record RabbitMqQueuePurgeItem(
    string QueueName,
    long ReadyBefore,
    RabbitMqExecutionStatus Status,
    string Message);

public sealed record RabbitMqQueuePurgeSummary(
    string VirtualHost,
    IReadOnlyList<RabbitMqQueuePurgeItem> Items,
    long ReadyAfter,
    long UnackedAfter,
    bool Cancelled)
{
    public int SucceededCount => Items.Count(item => item.Status == RabbitMqExecutionStatus.Succeeded);
    public int FailedCount => Items.Count(item => item.Status == RabbitMqExecutionStatus.Failed);
}
