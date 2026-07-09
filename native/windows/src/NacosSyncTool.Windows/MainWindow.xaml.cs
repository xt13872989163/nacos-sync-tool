using System.Windows;
using System.Windows.Controls;
using NacosSyncTool.Windows.ViewModels;

namespace NacosSyncTool.Windows;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();

        // 获取 ViewModel 并设置为 DataContext
        DataContext = App.ServiceProvider.GetService(typeof(MainViewModel));
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeSelector.SelectedItem is ComboBoxItem item && item.Tag is string themeName)
        {
            App.SwitchTheme(themeName);
        }
    }

    private void OnSourcePasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox)
        {
            ViewModel.SourcePassword = passwordBox.Password;
        }
    }

    private void OnTargetPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox)
        {
            ViewModel.TargetPassword = passwordBox.Password;
        }
    }
}
