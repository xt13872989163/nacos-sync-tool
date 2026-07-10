using System.Windows;
using NacosSyncTool.Windows.Models;

namespace NacosSyncTool.Windows.Views;

/// <summary>
/// 确认对话框
/// 用于覆盖确认、Namespace 同步双重确认等
/// </summary>
public partial class ConfirmDialog : Window
{
    public ConfirmDecision Decision { get; private set; } = ConfirmDecision.Cancel;

    public ConfirmDialog(ConfirmationRequest request)
    {
        InitializeComponent();
        LoadRequest(request);
    }

    private void LoadRequest(ConfirmationRequest request)
    {
        Title = request.Title;
        TitleText.Text = request.Title;
        BodyText.Text = request.Body;

        // 显示受影响的配置列表
        if (request.AffectedConfigs.Count > 0)
        {
            AffectedPanel.Visibility = Visibility.Visible;
            var configList = string.Join("\n",
                request.AffectedConfigs.Select(c => $"  • {c.DataId} / {c.Group}"));
            AffectedText.Text = $"受影响的配置（共 {request.AffectedFileCount} 个）：\n{configList}";
        }
        else
        {
            AffectedPanel.Visibility = Visibility.Collapsed;
        }

        // 根据确认类型调整按钮
        switch (request.Kind)
        {
            case ConfirmationKind.NamespaceStart:
                ConfirmButton.Content = "开始同步";
                SkipButton.Visibility = Visibility.Collapsed;
                break;
            case ConfirmationKind.OverwriteExistingFiles:
                ConfirmButton.Content = "确认覆盖";
                SkipButton.Content = "跳过覆盖";
                break;
            case ConfirmationKind.OverwriteFile:
                ConfirmButton.Content = "覆盖";
                SkipButton.Content = "跳过";
                break;
            case ConfirmationKind.OverwriteKey:
                ConfirmButton.Content = "覆盖";
                SkipButton.Content = "跳过";
                break;
        }
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        Decision = ConfirmDecision.Confirm;
        DialogResult = true;
        Close();
    }

    private void OnSkipClick(object sender, RoutedEventArgs e)
    {
        Decision = ConfirmDecision.Skip;
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Decision = ConfirmDecision.Cancel;
        DialogResult = false;
        Close();
    }

    /// <summary>
    /// 显示确认对话框并返回决策
    /// </summary>
    public static async Task<ConfirmDecision> ShowAsync(ConfirmationRequest request)
    {
        ConfirmDecision decision = ConfirmDecision.Cancel;

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var dialog = new ConfirmDialog(request);
            dialog.ShowDialog();
            decision = dialog.Decision;
        });

        return decision;
    }
}
