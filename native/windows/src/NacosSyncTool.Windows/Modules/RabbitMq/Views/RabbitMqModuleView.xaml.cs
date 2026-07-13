using System.Windows;
using System.Windows.Controls;
using NacosSyncTool.Windows.Modules.RabbitMq.ViewModels;

namespace NacosSyncTool.Windows.Modules.RabbitMq.Views;

public partial class RabbitMqModuleView : UserControl
{
    public event EventHandler? BackToNacosRequested;

    private RabbitMqModuleViewModel ViewModel => (RabbitMqModuleViewModel)DataContext;

    public RabbitMqModuleView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel.InitializeCommand.CanExecute(null)) ViewModel.InitializeCommand.Execute(null);
    }

    private void OnBackToNacosClick(object sender, RoutedEventArgs e) =>
        BackToNacosRequested?.Invoke(this, EventArgs.Empty);

    private void OnSourcePasswordChanged(object sender, RoutedEventArgs e) =>
        ViewModel.SourcePassword = ((PasswordBox)sender).Password;

    private void OnTargetPasswordChanged(object sender, RoutedEventArgs e) =>
        ViewModel.TargetPassword = ((PasswordBox)sender).Password;

    private void OnPurgePasswordChanged(object sender, RoutedEventArgs e) =>
        ViewModel.PurgePassword = ((PasswordBox)sender).Password;
}
