using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using NacosSyncTool.Windows.Models;
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

    private void OnScanKeyEnter(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ViewModel.ScanKeyCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnSelectableDataGridPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ShouldIgnoreRowToggle(e.OriginalSource as DependencyObject))
        {
            return;
        }

        var row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row?.DataContext is NacosConfigItem configItem)
        {
            configItem.IsSelected = !configItem.IsSelected;
            e.Handled = true;
            return;
        }

        if (row?.DataContext is KeyScanResult keyScanResult)
        {
            keyScanResult.IsSelected = !keyScanResult.IsSelected;
            e.Handled = true;
        }
    }

    private void OnCopyValueClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string text })
        {
            return;
        }

        try
        {
            Clipboard.SetText(text);
            ViewModel.NotifyValueCopied();
        }
        catch (Exception ex)
        {
            ViewModel.NotifyValueCopyFailed(ex.Message);
        }

        e.Handled = true;
    }

    private static bool ShouldIgnoreRowToggle(DependencyObject? source)
    {
        for (var current = source; current != null; current = GetVisualParent(current))
        {
            if (current is ButtonBase or ComboBox or TextBox or DataGridColumnHeader or ScrollBar)
            {
                return true;
            }

            if (current is DataGridRow)
            {
                return false;
            }
        }

        return false;
    }

    private static T? FindVisualParent<T>(DependencyObject? source) where T : DependencyObject
    {
        for (var current = source; current != null; current = GetVisualParent(current))
        {
            if (current is T target)
            {
                return target;
            }
        }

        return null;
    }

    private static DependencyObject? GetVisualParent(DependencyObject source)
    {
        try
        {
            return VisualTreeHelper.GetParent(source);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
