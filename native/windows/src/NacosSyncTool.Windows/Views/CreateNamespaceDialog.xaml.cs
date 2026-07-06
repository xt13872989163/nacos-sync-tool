using System.Windows;

namespace NacosSyncTool.Windows.Views;

/// <summary>
/// 新建 Namespace 对话框
/// </summary>
public partial class CreateNamespaceDialog : Window
{
    public string NamespaceId => NamespaceIdBox.Text.Trim();
    public string NamespaceName => NamespaceNameBox.Text.Trim();
    public string NamespaceDesc => NamespaceDescBox.Text.Trim();

    public CreateNamespaceDialog()
    {
        InitializeComponent();
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NamespaceId))
        {
            MessageBox.Show("请输入 Namespace ID", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(NamespaceName))
        {
            MessageBox.Show("请输入 Namespace 名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
