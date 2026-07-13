namespace NacosSyncTool.Windows.Modules.RabbitMq.Models;

public enum RabbitMqResourceType
{
    VirtualHost,
    Exchange,
    Policy,
    Queue,
    Binding
}

public enum RabbitMqPlanStatus
{
    PendingCreate,
    ExistingSkip,
    Unsupported,
    MissingDependency,
    Unknown
}

public enum RabbitMqExecutionStatus
{
    Created,
    Succeeded,
    Skipped,
    Failed,
    Cancelled,
    Missing
}

public sealed record RabbitMqSyncPlanItem(
    RabbitMqResourceType Type,
    string Identity,
    object Resource,
    RabbitMqPlanStatus Status,
    string Reason);

public sealed record RabbitMqSyncPlan(
    string VirtualHost,
    DateTimeOffset CreatedAt,
    IReadOnlyList<RabbitMqSyncPlanItem> Items)
{
    public int PendingCount => Items.Count(item => item.Status == RabbitMqPlanStatus.PendingCreate);
    public int SkippedCount => Items.Count(item => item.Status == RabbitMqPlanStatus.ExistingSkip);
    public int ProblemCount => Items.Count(item => item.Status is RabbitMqPlanStatus.Unsupported or RabbitMqPlanStatus.MissingDependency or RabbitMqPlanStatus.Unknown);
}

public sealed record RabbitMqExecutionItem(
    RabbitMqResourceType Type,
    string Identity,
    RabbitMqExecutionStatus Status,
    string Message);

public sealed record RabbitMqExecutionSummary(
    IReadOnlyList<RabbitMqExecutionItem> Items)
{
    public int CreatedCount => Items.Count(item => item.Status == RabbitMqExecutionStatus.Created);
    public int SkippedCount => Items.Count(item => item.Status == RabbitMqExecutionStatus.Skipped);
    public int FailedCount => Items.Count(item => item.Status == RabbitMqExecutionStatus.Failed);
}
