using NacosSyncTool.Windows.Modules.RabbitMq.Models;

namespace NacosSyncTool.Windows.Modules.RabbitMq.Services;

public interface IRabbitMqConfirmationService
{
    Task<bool> ConfirmAsync(RabbitMqConfirmationRequest request);
}
