using NacosSyncTool.Windows.Modules.RabbitMq.Models;

namespace NacosSyncTool.Windows.Modules.RabbitMq.Services;

public interface IRabbitMqClientFactory
{
    IRabbitMqManagementClient Create(
        RabbitMqConnectionSettings settings,
        RabbitMqClientRole role);
}

public sealed class RabbitMqClientFactory : IRabbitMqClientFactory
{
    public IRabbitMqManagementClient Create(
        RabbitMqConnectionSettings settings,
        RabbitMqClientRole role) =>
        new RabbitMqManagementClient(settings, role);
}
