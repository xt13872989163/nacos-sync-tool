using System.Windows;
using NacosSyncTool.Windows.Modules.RabbitMq.Models;
using NacosSyncTool.Windows.Modules.RabbitMq.Views;

namespace NacosSyncTool.Windows.Modules.RabbitMq.Services;

public sealed class RabbitMqConfirmationService : IRabbitMqConfirmationService
{
    public async Task<bool> ConfirmAsync(RabbitMqConfirmationRequest request)
    {
        var confirmed = false;
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var dialog = new RabbitMqConfirmDialog(request)
            {
                Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive)
            };
            confirmed = dialog.ShowDialog() == true;
        });
        return confirmed;
    }
}
