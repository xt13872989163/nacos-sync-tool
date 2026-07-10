using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;
using NacosSyncTool.Windows.Models;
using NacosSyncTool.Windows.Services;
using NacosSyncTool.Windows.Views;

namespace NacosSyncTool.Windows.ViewModels;

/// <summary>
/// 主窗口 ViewModel
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly LogService _logService;
    private readonly NacosApiService _sourceNacosApi;
    private readonly NacosApiService _targetNacosApi;
    private CancellationTokenSource? _toastCancellation;

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

    // ===== 同步模式 =====
    [ObservableProperty]
    private SyncMode _syncMode = SyncMode.File;

    // ===== 配置列表（文件级别） =====
    [ObservableProperty]
    private ObservableCollection<NacosConfigItem> _configList = new();

    [ObservableProperty]
    private NacosConfigItem? _selectedConfig;

    [ObservableProperty]
    private List<NacosConfigItem> _selectedConfigs = new();

    // ===== Key 扫描 =====
    [ObservableProperty]
    private string _keyName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<KeyScanResult> _keyScanResults = new();

    // ===== 操作状态 =====
    [ObservableProperty]
    private bool _isBusy = false;

    [ObservableProperty]
    private string _busyText = string.Empty;

    [ObservableProperty]
    private bool _isToastVisible = false;

    [ObservableProperty]
    private string _toastMessage = string.Empty;

    // ===== 结果摘要 =====
    [ObservableProperty]
    private int _selectedConfigCount;

    [ObservableProperty]
    private int _selectedKeyCount;

    [ObservableProperty]
    private int _selectedKeyOnlyCount;

    [ObservableProperty]
    private int _selectedFullFileCount;

    [ObservableProperty]
    private string _fileListSummary = string.Empty;

    [ObservableProperty]
    private string _keyListSummary = string.Empty;

    [ObservableProperty]
    private bool _hasFileResults;

    [ObservableProperty]
    private bool _hasKeyResults;

    // ===== 日志 =====
    [ObservableProperty]
    private ObservableCollection<LogEntry> _logs = new();

    public MainViewModel(LogService logService)
    {
        _logService = logService;
        _sourceNacosApi = new NacosApiService();
        _targetNacosApi = new NacosApiService();

        _logService.LogAdded += OnLogAdded;
        _logService.Info("应用启动成功");

        ConfigList.CollectionChanged += OnConfigListChanged;
        KeyScanResults.CollectionChanged += OnKeyScanResultsChanged;
        UpdateFileListSummary();
        UpdateKeyListSummary();
    }

    // ===== 批量选择 =====

    [RelayCommand]
    private void SelectAllFiles()
    {
        foreach (var item in ConfigList) item.IsSelected = true;
        RefreshFileSummaries();
    }

    [RelayCommand]
    private void InvertFileSelection()
    {
        foreach (var item in ConfigList) item.IsSelected = !item.IsSelected;
        RefreshFileSummaries();
    }

    [RelayCommand]
    private void ClearFileSelection()
    {
        foreach (var item in ConfigList) item.IsSelected = false;
        RefreshFileSummaries();
    }

    [RelayCommand]
    private void SelectAllKeys()
    {
        foreach (var item in KeyScanResults) item.IsSelected = true;
        RefreshKeySummaries();
    }

    [RelayCommand]
    private void InvertKeySelection()
    {
        foreach (var item in KeyScanResults) item.IsSelected = !item.IsSelected;
        RefreshKeySummaries();
    }

    [RelayCommand]
    private void ClearKeySelection()
    {
        foreach (var item in KeyScanResults) item.IsSelected = false;
        RefreshKeySummaries();
    }

    [RelayCommand]
    private void SetAllKeyStrategyKeyOnly()
    {
        foreach (var item in KeyScanResults) item.SyncStrategy = KeySyncStrategy.KeyOnly;
        RefreshKeySummaries();
    }

    [RelayCommand]
    private void SetAllKeyStrategyFullFile()
    {
        foreach (var item in KeyScanResults) item.SyncStrategy = KeySyncStrategy.FullFile;
        RefreshKeySummaries();
    }

    // ===== 源端连接 =====

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
                _logService.Success("源端连接成功");
                IsSourceConnected = true;
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

        var result = await _sourceNacosApi.GetNamespacesAsync();

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

        var result = await _targetNacosApi.GetNamespacesAsync();

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

        var dialog = new CreateNamespaceDialog { Owner = GetMainWindow() };
        if (dialog.ShowDialog() == true)
        {
            var namespaceId = dialog.NamespaceId;
            var namespaceName = dialog.NamespaceName;
            var namespaceDesc = dialog.NamespaceDesc;

            _logService.Info($"正在创建 Namespace: {namespaceId}...");

            var result = await _targetNacosApi.CreateNamespaceAsync(namespaceId, namespaceName, namespaceDesc);

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

    public void NotifyValueCopied()
    {
        _logService.Success("原值已复制到剪贴板");
        ShowToast("原值已复制到剪贴板");
    }

    public void NotifyValueCopyFailed(string message)
    {
        _logService.Error($"复制原值失败: {message}");
        ShowToast("复制失败，请重试");
    }

    private void ShowToast(string message)
    {
        _ = ShowToastAsync(message);
    }

    private async Task ShowToastAsync(string message)
    {
        _toastCancellation?.Cancel();
        _toastCancellation?.Dispose();

        var cancellation = new CancellationTokenSource();
        _toastCancellation = cancellation;
        ToastMessage = message;
        IsToastVisible = true;

        try
        {
            await Task.Delay(2200, cancellation.Token);
            if (_toastCancellation == cancellation)
            {
                IsToastVisible = false;
            }
        }
        catch (TaskCanceledException)
        {
            // 新提示会取消旧提示的隐藏任务。
        }
        finally
        {
            if (_toastCancellation == cancellation)
            {
                _toastCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    // ===== 数据加载 =====

    /// <summary>
    /// 加载源端配置列表（文件级别同步用）
    /// </summary>
    [RelayCommand]
    private async Task LoadConfigListAsync()
    {
        if (!IsSourceConnected || SelectedSourceNamespace == null)
        {
            _logService.Warning("请先连接源端并选择 Namespace");
            return;
        }

        IsBusy = true;
        BusyText = "正在加载配置列表...";
        _logService.Info($"正在加载配置列表：{SelectedSourceNamespace.DisplayText}");

        try
        {
            var result = await _sourceNacosApi.ListConfigsAsync(SelectedSourceNamespace.NamespaceId);

            if (result.Success && result.Data != null)
            {
                ConfigList.Clear();
                foreach (var item in result.Data)
                {
                    ConfigList.Add(item);
                }
                _logService.Success($"成功加载 {result.Data.Count} 个配置文件");
            }
            else
            {
                _logService.Error($"加载配置列表失败: {result.Message}");
            }
        }
        catch (Exception ex)
        {
            _logService.Error($"加载配置列表异常: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ===== Key 扫描 =====

    /// <summary>
    /// 扫描 Key
    /// </summary>
    [RelayCommand]
    private async Task ScanKeyAsync()
    {
        if (!IsSourceConnected || SelectedSourceNamespace == null)
        {
            _logService.Warning("请先连接源端并选择 Namespace");
            return;
        }

        if (string.IsNullOrWhiteSpace(KeyName))
        {
            _logService.Warning("请输入要扫描的 Key 名称");
            return;
        }

        IsBusy = true;
        BusyText = $"正在扫描 Key: {KeyName}...";

        try
        {
            var syncService = CreateSyncService();
            var results = await syncService.ScanKeyAsync(SelectedSourceNamespace.NamespaceId, KeyName);

            KeyScanResults.Clear();
            foreach (var r in results)
            {
                KeyScanResults.Add(r);
            }

            _logService.Success($"扫描完成，共找到 {results.Count} 条结果");
        }
        catch (Exception ex)
        {
            _logService.Error($"Key 扫描异常: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ===== 同步操作 =====

    /// <summary>
    /// 执行同步（根据当前模式）
    /// </summary>
    [RelayCommand]
    private async Task SyncAsync()
    {
        if (!ValidateSyncPrerequisites()) return;

        // 同步前摘要确认
        var selectedFileCount = ConfigList.Count(c => c.IsSelected);
        var selectedKeyResults = KeyScanResults.Where(r => r.IsSelected).ToList();
        if (SyncMode == SyncMode.File && selectedFileCount == 0)
        {
            _logService.Warning("请先选择要同步的配置文件");
            return;
        }
        if (SyncMode == SyncMode.Key && selectedKeyResults.Count == 0)
        {
            _logService.Warning("请先选择要同步的扫描结果");
            return;
        }

        if (!ShowSyncSummaryConfirmation(selectedFileCount, selectedKeyResults)) return;

        IsBusy = true;

        try
        {
            var syncService = CreateSyncService();
            SyncSummary summary;

            switch (SyncMode)
            {
                case SyncMode.Namespace:
                    BusyText = "正在同步 Namespace...";
                    summary = await syncService.SyncNamespaceAsync(
                        SelectedSourceNamespace!.NamespaceId,
                        SelectedTargetNamespace!.NamespaceId);
                    break;

                case SyncMode.File:
                    var selectedFiles = ConfigList.Where(c => c.IsSelected).ToList();
                    BusyText = $"正在同步 {selectedFiles.Count} 个文件...";
                    summary = await syncService.SyncFilesAsync(
                        SelectedTargetNamespace!.NamespaceId,
                        selectedFiles);
                    break;

                case SyncMode.Key:
                    var selectedResults = selectedKeyResults;
                    BusyText = $"正在同步 {selectedResults.Count} 条 Key...";
                    summary = await syncService.SyncKeyResultsAsync(
                        SelectedSourceNamespace!.NamespaceId,
                        SelectedTargetNamespace!.NamespaceId,
                        selectedResults);
                    break;

                default:
                    return;
            }

            _logService.Success($"同步完成：{summary}");
        }
        catch (Exception ex)
        {
            _logService.Error($"同步异常: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 验证同步前置条件
    /// </summary>
    private bool ValidateSyncPrerequisites()
    {
        if (!IsSourceConnected || !IsTargetConnected)
        {
            _logService.Warning("请先连接源端和目标端 Nacos");
            return false;
        }

        if (SelectedSourceNamespace == null || SelectedTargetNamespace == null)
        {
            _logService.Warning("请选择源端和目标端 Namespace");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 显示同步前摘要确认
    /// </summary>
    private bool ShowSyncSummaryConfirmation(int selectedFileCount, List<KeyScanResult> selectedKeyResults)
    {
        var modeLabel = SyncMode switch
        {
            SyncMode.Namespace => "Namespace 级别",
            SyncMode.File => "文件级别",
            SyncMode.Key => "Key 级别",
            _ => SyncMode.ToString()
        };

        var sourceNs = SelectedSourceNamespace?.DisplayText ?? "(未选择)";
        var targetNs = SelectedTargetNamespace?.DisplayText ?? "(未选择)";

        var lines = new List<string>
        {
            $"同步模式：{modeLabel}",
            $"源端 Namespace：{sourceNs}",
            $"目标端 Namespace：{targetNs}"
        };

        if (SyncMode == SyncMode.File)
        {
            lines.Add($"已选文件：{selectedFileCount} 个");
        }
        else if (SyncMode == SyncMode.Key)
        {
            var keyOnly = selectedKeyResults.Count(r => r.SyncStrategy == KeySyncStrategy.KeyOnly);
            var fullFile = selectedKeyResults.Count(r => r.SyncStrategy == KeySyncStrategy.FullFile);
            lines.Add($"已选结果：{selectedKeyResults.Count} 条");
            lines.Add($"  仅同步 Key：{keyOnly} 条");
            lines.Add($"  同步整个文件：{fullFile} 条");
        }

        var body = string.Join("\n", lines);
        var request = new ConfirmationRequest
        {
            Kind = ConfirmationKind.NamespaceStart,
            Title = "确认开始同步",
            Body = body
        };

        var decision = ConfirmDialog.ShowAsync(request).GetAwaiter().GetResult();
        return decision == ConfirmDecision.Confirm;
    }

    /// <summary>
    /// 创建同步服务实例
    /// </summary>
    private SyncService CreateSyncService()
    {
        var sourceClient = new SyncNacosClientAdapter(_sourceNacosApi);
        var targetClient = new SyncNacosClientAdapter(_targetNacosApi);

        ConfirmHandler confirm = request => ConfirmDialog.ShowAsync(request);

        return new SyncService(sourceClient, targetClient, confirm, msg => _logService.Info(msg));
    }

    // ===== 摘要刷新 =====

    private void OnConfigListChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (NacosConfigItem item in e.OldItems)
                item.PropertyChanged -= OnConfigItemPropertyChanged;

        if (e.NewItems != null)
            foreach (NacosConfigItem item in e.NewItems)
                item.PropertyChanged += OnConfigItemPropertyChanged;

        HasFileResults = ConfigList.Count > 0;
        RefreshFileSummaries();
    }

    private void OnKeyScanResultsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (KeyScanResult item in e.OldItems)
                item.PropertyChanged -= OnKeyItemPropertyChanged;

        if (e.NewItems != null)
            foreach (KeyScanResult item in e.NewItems)
                item.PropertyChanged += OnKeyItemPropertyChanged;

        HasKeyResults = KeyScanResults.Count > 0;
        RefreshKeySummaries();
    }

    private void OnConfigItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NacosConfigItem.IsSelected))
            RefreshFileSummaries();
    }

    private void OnKeyItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(KeyScanResult.IsSelected) || e.PropertyName == nameof(KeyScanResult.SyncStrategy))
            RefreshKeySummaries();
    }

    private void RefreshFileSummaries()
    {
        SelectedConfigCount = ConfigList.Count(c => c.IsSelected);
        UpdateFileListSummary();
    }

    private void RefreshKeySummaries()
    {
        var selected = KeyScanResults.Where(r => r.IsSelected).ToList();
        SelectedKeyCount = selected.Count;
        SelectedKeyOnlyCount = selected.Count(r => r.SyncStrategy == KeySyncStrategy.KeyOnly);
        SelectedFullFileCount = selected.Count(r => r.SyncStrategy == KeySyncStrategy.FullFile);
        UpdateKeyListSummary();
    }

    private void UpdateFileListSummary()
    {
        var total = ConfigList.Count;
        var selected = ConfigList.Count(c => c.IsSelected);
        FileListSummary = total == 0 ? string.Empty : $"共 {total} 个文件，已选 {selected}";
    }

    private void UpdateKeyListSummary()
    {
        var total = KeyScanResults.Count;
        var selected = KeyScanResults.Count(r => r.IsSelected);
        KeyListSummary = total == 0 ? string.Empty : $"共 {total} 条结果，已选 {selected}";
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

    private static Window? GetMainWindow()
    {
        return Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
    }
}
