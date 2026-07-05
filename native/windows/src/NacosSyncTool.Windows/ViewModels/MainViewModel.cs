using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;
using NacosSyncTool.Windows.Models;
using NacosSyncTool.Windows.Services;

namespace NacosSyncTool.Windows.ViewModels;

/// <summary>
/// 主窗口 ViewModel
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly NacosApiService _sourceNacosApi;
    private readonly NacosApiService _targetNacosApi;
    private readonly LogService _logService;

    // ===== 源端 Nacos =====
    [ObservableProperty]
    private string _sourceAddress = string.Empty;

    [ObservableProperty]
    private string _sourceUsername = string.Empty;

    [ObservableProperty]
    private string _sourcePassword = string.Empty;

    [ObservableProperty]
    private bool _isSourceConnected = false;

    [ObservableProperty]
    private bool _isSourceConnecting = false;

    [ObservableProperty]
    private ObservableCollection<NacosNamespace> _sourceNamespaces = new();

    [ObservableProperty]
    private NacosNamespace? _selectedSourceNamespace;

    // ===== 目标端 Nacos =====
    [ObservableProperty]
    private string _targetAddress = string.Empty;

    [ObservableProperty]
    private string _targetUsername = string.Empty;

    [ObservableProperty]
    private string _targetPassword = string.Empty;

    [ObservableProperty]
    private bool _isTargetConnected = false;

    [ObservableProperty]
    private bool _isTargetConnecting = false;

    [ObservableProperty]
    private ObservableCollection<NacosNamespace> _targetNamespaces = new();

    [ObservableProperty]
    private NacosNamespace? _selectedTargetNamespace;

    // ===== 日志 =====
    [ObservableProperty]
    private ObservableCollection<LogEntry> _logs = new();

    public MainViewModel(LogService logService)
    {
        _logService = logService;
        _sourceNacosApi = new NacosApiService();
        _targetNacosApi = new NacosApiService();

        // 订阅日志事件
        _logService.LogAdded += OnLogAdded;

        _logService.Info("应用启动成功");
    }

    /// <summary>
    /// 测试源端连接
    /// </summary>
    [RelayCommand]
    private async Task TestSourceConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(SourceAddress))
        {
            _logService.Warning("请输入源端地址");
            return;
        }

        if (string.IsNullOrWhiteSpace(SourceUsername) || string.IsNullOrWhiteSpace(SourcePassword))
        {
            _logService.Warning("请输入源端账号和密码");
            return;
        }

        IsSourceConnecting = true;
        _logService.Info($"正在连接源端 Nacos: {SourceAddress}...");

        try
        {
            var result = await _sourceNacosApi.TestConnectionAsync(SourceAddress, SourceUsername, SourcePassword);

            if (result.Success)
            {
                _logService.Success($"源端连接成功");
                IsSourceConnected = true;

                // 自动拉取 Namespace 列表
                await LoadSourceNamespacesAsync();
            }
            else
            {
                _logService.Error($"源端连接失败: {result.Message}");
                IsSourceConnected = false;
            }
        }
        catch (Exception ex)
        {
            _logService.Error($"源端连接异常: {ex.Message}");
            IsSourceConnected = false;
        }
        finally
        {
            IsSourceConnecting = false;
        }
    }

    /// <summary>
    /// 加载源端 Namespace 列表
    /// </summary>
    private async Task LoadSourceNamespacesAsync()
    {
        _logService.Info("正在拉取源端 Namespace 列表...");

        var result = await _sourceNacosApi.GetNamespacesAsync(SourceAddress);

        if (result.Success && result.Data != null)
        {
            SourceNamespaces.Clear();
            foreach (var ns in result.Data)
            {
                SourceNamespaces.Add(ns);
            }

            _logService.Success($"成功获取 {result.Data.Count} 个 Namespace");
        }
        else
        {
            _logService.Error($"获取源端 Namespace 失败: {result.Message}");
        }
    }

    /// <summary>
    /// 测试目标端连接
    /// </summary>
    [RelayCommand]
    private async Task TestTargetConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(TargetAddress))
        {
            _logService.Warning("请输入目标端地址");
            return;
        }

        if (string.IsNullOrWhiteSpace(TargetUsername) || string.IsNullOrWhiteSpace(TargetPassword))
        {
            _logService.Warning("请输入目标端账号和密码");
            return;
        }

        IsTargetConnecting = true;
        _logService.Info($"正在连接目标端 Nacos: {TargetAddress}...");

        try
        {
            var result = await _targetNacosApi.TestConnectionAsync(TargetAddress, TargetUsername, TargetPassword);

            if (result.Success)
            {
                _logService.Success($"目标端连接成功");
                IsTargetConnected = true;

                // 自动拉取 Namespace 列表
                await LoadTargetNamespacesAsync();
            }
            else
            {
                _logService.Error($"目标端连接失败: {result.Message}");
                IsTargetConnected = false;
            }
        }
        catch (Exception ex)
        {
            _logService.Error($"目标端连接异常: {ex.Message}");
            IsTargetConnected = false;
        }
        finally
        {
            IsTargetConnecting = false;
        }
    }

    /// <summary>
    /// 加载目标端 Namespace 列表
    /// </summary>
    private async Task LoadTargetNamespacesAsync()
    {
        _logService.Info("正在拉取目标端 Namespace 列表...");

        var result = await _targetNacosApi.GetNamespacesAsync(TargetAddress);

        if (result.Success && result.Data != null)
        {
            TargetNamespaces.Clear();
            foreach (var ns in result.Data)
            {
                TargetNamespaces.Add(ns);
            }

            _logService.Success($"成功获取 {result.Data.Count} 个 Namespace");
        }
        else
        {
            _logService.Error($"获取目标端 Namespace 失败: {result.Message}");
        }
    }

    /// <summary>
    /// 创建目标端 Namespace
    /// </summary>
    [RelayCommand]
    private async Task CreateTargetNamespaceAsync()
    {
        if (!IsTargetConnected)
        {
            _logService.Warning("请先连接目标端 Nacos");
            return;
        }

        // 这里应该弹出对话框让用户输入，暂时用测试数据
        var dialog = new CreateNamespaceDialog();
        if (dialog.ShowDialog() == true)
        {
            var namespaceId = dialog.NamespaceId;
            var namespaceName = dialog.NamespaceName;
            var namespaceDesc = dialog.NamespaceDesc;

            _logService.Info($"正在创建 Namespace: {namespaceId}...");

            var result = await _targetNacosApi.CreateNamespaceAsync(TargetAddress, namespaceId, namespaceName, namespaceDesc);

            if (result.Success)
            {
                _logService.Success($"Namespace 创建成功: {namespaceId}");
                await LoadTargetNamespacesAsync();

                // 自动选中新创建的 Namespace
                SelectedTargetNamespace = TargetNamespaces.FirstOrDefault(ns => ns.NamespaceId == namespaceId);
            }
            else
            {
                _logService.Error($"创建 Namespace 失败: {result.Message}");
            }
        }
    }

    /// <summary>
    /// 清空日志
    /// </summary>
    [RelayCommand]
    private void ClearLogs()
    {
        Logs.Clear();
        _logService.Info("日志已清空");
    }

    private void OnLogAdded(LogEntry log)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Logs.Add(log);

            // 限制日志数量，避免内存溢出
            while (Logs.Count > 1000)
            {
                Logs.RemoveAt(0);
            }
        });
    }
}

// 临时占位类 - 后续会实现完整的对话框
public class CreateNamespaceDialog : Window
{
    public string NamespaceId { get; set; } = string.Empty;
    public string NamespaceName { get; set; } = string.Empty;
    public string NamespaceDesc { get; set; } = string.Empty;
}
