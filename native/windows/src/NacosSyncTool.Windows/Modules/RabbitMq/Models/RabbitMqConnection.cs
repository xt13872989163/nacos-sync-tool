namespace NacosSyncTool.Windows.Modules.RabbitMq.Models;

public enum RabbitMqClientRole
{
    SourceReadOnly,
    TargetTopology,
    QueuePurge
}

public enum RabbitMqOperationMode
{
    TopologySync,
    QueuePurge
}

public enum RabbitMqSelectionMode
{
    WholeVirtualHost,
    SelectedResources
}

public sealed record RabbitMqConnectionSettings(
    string Address,
    string Username,
    string Password,
    TimeSpan Timeout,
    bool ValidateServerCertificate = true);

public sealed record RabbitMqConnectionInfo(
    string RabbitMqVersion,
    string ClusterName);
