using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NacosSyncTool.Windows.Modules.RabbitMq.Models;
using NacosSyncTool.Windows.Modules.RabbitMq.Services;

namespace NacosSyncTool.Windows.Modules.RabbitMq.ViewModels;

public partial class RabbitMqModuleViewModel : ObservableObject
{
    private readonly IRabbitMqClientFactory _clientFactory;
    private readonly RabbitMqPlanBuilder _planBuilder;
    private readonly RabbitMqSettingsStore _settingsStore;
    private readonly RabbitMqLogService _logService;
    private readonly IRabbitMqConfirmationService _confirmationService;
    private IRabbitMqManagementClient? _sourceClient;
    private IRabbitMqManagementClient? _targetClient;
    private IRabbitMqManagementClient? _purgeClient;
    private RabbitMqTopologySnapshot? _sourceSnapshot;
    private CancellationTokenSource? _operationCancellation;

    [ObservableProperty] private RabbitMqOperationMode _operationMode = RabbitMqOperationMode.TopologySync;
    [ObservableProperty] private RabbitMqSelectionMode _selectionMode = RabbitMqSelectionMode.WholeVirtualHost;
    [ObservableProperty] private RabbitMqResourceType _selectedResourceTab = RabbitMqResourceType.Exchange;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _busyText = string.Empty;
    [ObservableProperty] private string _progressText = string.Empty;

    [ObservableProperty] private string _sourceAddress = string.Empty;
    [ObservableProperty] private string _sourceUsername = string.Empty;
    [ObservableProperty] private string _sourcePassword = string.Empty;
    [ObservableProperty] private bool _isSourceConnected;
    [ObservableProperty] private string _sourceConnectionInfo = string.Empty;
    [ObservableProperty] private ObservableCollection<RabbitMqVirtualHost> _sourceVirtualHosts = [];
    [ObservableProperty] private RabbitMqVirtualHost? _selectedSourceVirtualHost;

    [ObservableProperty] private string _targetAddress = string.Empty;
    [ObservableProperty] private string _targetUsername = string.Empty;
    [ObservableProperty] private string _targetPassword = string.Empty;
    [ObservableProperty] private bool _isTargetConnected;
    [ObservableProperty] private string _targetConnectionInfo = string.Empty;
    [ObservableProperty] private ObservableCollection<RabbitMqVirtualHost> _targetVirtualHosts = [];
    [ObservableProperty] private string _targetVirtualHostName = string.Empty;
    [ObservableProperty] private string _targetVirtualHostStatus = "未选择源 Virtual Host";

    [ObservableProperty] private ObservableCollection<RabbitMqSelectionItem<RabbitMqExchange>> _exchanges = [];
    [ObservableProperty] private ObservableCollection<RabbitMqSelectionItem<RabbitMqQueue>> _queues = [];
    [ObservableProperty] private ObservableCollection<RabbitMqSelectionItem<RabbitMqBinding>> _bindings = [];
    [ObservableProperty] private ObservableCollection<RabbitMqSelectionItem<RabbitMqPolicy>> _policies = [];
    [ObservableProperty] private RabbitMqSyncPlan? _currentPlan;
    [ObservableProperty] private string _planSummary = "请先加载资源并生成同步预览";
    [ObservableProperty] private string _executionSummary = string.Empty;

    [ObservableProperty] private string _purgeAddress = string.Empty;
    [ObservableProperty] private string _purgeUsername = string.Empty;
    [ObservableProperty] private string _purgePassword = string.Empty;
    [ObservableProperty] private bool _isPurgeConnected;
    [ObservableProperty] private string _purgeConnectionInfo = string.Empty;
    [ObservableProperty] private ObservableCollection<RabbitMqVirtualHost> _purgeVirtualHosts = [];
    [ObservableProperty] private RabbitMqVirtualHost? _selectedPurgeVirtualHost;
    [ObservableProperty] private ObservableCollection<RabbitMqQueue> _purgeQueues = [];
    [ObservableProperty] private long _readyTotal;
    [ObservableProperty] private long _unackedTotal;
    [ObservableProperty] private int _consumerTotal;
    [ObservableProperty] private string _purgeSummary = string.Empty;

    [ObservableProperty] private ObservableCollection<RabbitMqLogEntry> _logs = [];

    public bool IsTopologyMode => OperationMode == RabbitMqOperationMode.TopologySync;
    public bool IsPurgeMode => OperationMode == RabbitMqOperationMode.QueuePurge;

    public RabbitMqModuleViewModel(
        IRabbitMqClientFactory clientFactory,
        RabbitMqPlanBuilder planBuilder,
        RabbitMqSettingsStore settingsStore,
        RabbitMqLogService logService,
        IRabbitMqConfirmationService confirmationService)
    {
        _clientFactory = clientFactory;
        _planBuilder = planBuilder;
        _settingsStore = settingsStore;
        _logService = logService;
        _confirmationService = confirmationService;
        _logService.LogAdded += OnLogAdded;
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        await LoadSavedConnectionAsync(RabbitMqClientRole.SourceReadOnly);
        await LoadSavedConnectionAsync(RabbitMqClientRole.TargetTopology);
        await LoadSavedConnectionAsync(RabbitMqClientRole.QueuePurge);
    }

    [RelayCommand]
    private void ShowTopology() => OperationMode = RabbitMqOperationMode.TopologySync;

    [RelayCommand]
    private void ShowPurge() => OperationMode = RabbitMqOperationMode.QueuePurge;

    [RelayCommand]
    private async Task TestSourceConnectionAsync()
    {
        await RunBusyAsync("正在连接源 RabbitMQ...", async cancellationToken =>
        {
            _sourceClient = _clientFactory.Create(SourceSettings(), RabbitMqClientRole.SourceReadOnly);
            var info = await _sourceClient.TestConnectionAsync(cancellationToken);
            var virtualHosts = await _sourceClient.GetVirtualHostsAsync(cancellationToken);
            Replace(SourceVirtualHosts, virtualHosts);
            SourceConnectionInfo = $"{info.ClusterName} / RabbitMQ {info.RabbitMqVersion}";
            IsSourceConnected = true;
            await RestoreSelectedVirtualHostAsync(SourceVirtualHosts, RabbitMqClientRole.SourceReadOnly, value => SelectedSourceVirtualHost = value);
            await SaveConnectionAsync(RabbitMqClientRole.SourceReadOnly, SelectedSourceVirtualHost?.Name, cancellationToken);
            _logService.Success($"源 RabbitMQ 连接成功：{SourceConnectionInfo}");
        });
    }

    [RelayCommand]
    private async Task TestTargetConnectionAsync()
    {
        await RunBusyAsync("正在连接目标 RabbitMQ...", async cancellationToken =>
        {
            _targetClient = _clientFactory.Create(TargetSettings(), RabbitMqClientRole.TargetTopology);
            var info = await _targetClient.TestConnectionAsync(cancellationToken);
            var virtualHosts = await _targetClient.GetVirtualHostsAsync(cancellationToken);
            Replace(TargetVirtualHosts, virtualHosts);
            TargetConnectionInfo = $"{info.ClusterName} / RabbitMQ {info.RabbitMqVersion}";
            IsTargetConnected = true;
            RefreshTargetVirtualHostStatus();
            await SaveConnectionAsync(RabbitMqClientRole.TargetTopology, TargetVirtualHostName, cancellationToken);
            _logService.Success($"目标 RabbitMQ 连接成功：{TargetConnectionInfo}");
        });
    }

    [RelayCommand]
    private async Task RefreshResourcesAsync()
    {
        if (_sourceClient == null || SelectedSourceVirtualHost == null)
        {
            _logService.Warning("请先连接源 RabbitMQ 并选择 Virtual Host");
            return;
        }

        await RunBusyAsync("正在加载 RabbitMQ 资源...", async cancellationToken =>
        {
            _sourceSnapshot = await new RabbitMqResourceLoader(_sourceClient)
                .LoadSyncSnapshotAsync(SelectedSourceVirtualHost.Name, cancellationToken);
            SetSelections(Exchanges, _sourceSnapshot.Exchanges, value => value.Name);
            SetSelections(Queues, _sourceSnapshot.Queues, value => value.Name);
            SetSelections(Policies, _sourceSnapshot.Policies, value => value.Name);
            SetSelections(Bindings, _sourceSnapshot.Bindings, value => value.Identity);
            InvalidatePlan();
            _logService.Success($"资源加载完成：Exchange {_sourceSnapshot.Exchanges.Count}，Queue {_sourceSnapshot.Queues.Count}，Binding {_sourceSnapshot.Bindings.Count}，Policy {_sourceSnapshot.Policies.Count}");
        });
    }

    [RelayCommand]
    private async Task BuildPlanAsync()
    {
        if (_sourceSnapshot == null || _targetClient == null || SelectedSourceVirtualHost == null)
        {
            _logService.Warning("请先连接两端并加载源资源");
            return;
        }

        await RunBusyAsync("正在生成同步预览...", async cancellationToken =>
        {
            RabbitMqTopologySnapshot? targetSnapshot = null;
            if (TargetVirtualHosts.Any(item => string.Equals(item.Name, SelectedSourceVirtualHost.Name, StringComparison.Ordinal)))
            {
                targetSnapshot = await new RabbitMqResourceLoader(_targetClient)
                    .LoadSyncSnapshotAsync(SelectedSourceVirtualHost.Name, cancellationToken);
            }

            CurrentPlan = _planBuilder.Build(
                _sourceSnapshot,
                targetSnapshot,
                SelectedNames(Exchanges, SelectionMode),
                SelectedNames(Policies, SelectionMode),
                SelectedNames(Queues, SelectionMode),
                SelectedNames(Bindings, SelectionMode));
            PlanSummary = $"待创建 {CurrentPlan.PendingCount}｜跳过 {CurrentPlan.SkippedCount}｜异常 {CurrentPlan.ProblemCount}";
            _logService.Info($"同步预览已生成：{PlanSummary}");
        });
    }

    [RelayCommand(CanExecute = nameof(CanExecutePlan))]
    private async Task ExecutePlanAsync()
    {
        if (CurrentPlan == null || _targetClient == null) return;
        if (!await _confirmationService.ConfirmAsync(RabbitMqConfirmation.BuildSync(CurrentPlan))) return;

        await RunBusyAsync("正在执行 RabbitMQ 同步...", async cancellationToken =>
        {
            var progress = new Progress<(int Completed, int Total, string Current)>(value =>
                ProgressText = $"{value.Completed}/{value.Total} {value.Current}");
            var summary = await new RabbitMqSyncExecutor(_targetClient)
                .ExecuteAsync(CurrentPlan, progress, cancellationToken);
            ExecutionSummary = $"创建 {summary.CreatedCount}｜跳过 {summary.SkippedCount}｜失败 {summary.FailedCount}";
            _logService.Success($"RabbitMQ 同步结束：{ExecutionSummary}");
            CurrentPlan = null;
        });
    }

    [RelayCommand]
    private async Task RetryFailedAsync()
    {
        await BuildPlanAsync();
        if (CurrentPlan != null) await ExecutePlanAsync();
    }

    [RelayCommand]
    private async Task TestPurgeConnectionAsync()
    {
        await RunBusyAsync("正在连接 RabbitMQ 清理端...", async cancellationToken =>
        {
            _purgeClient = _clientFactory.Create(PurgeSettings(), RabbitMqClientRole.QueuePurge);
            var info = await _purgeClient.TestConnectionAsync(cancellationToken);
            var virtualHosts = await _purgeClient.GetVirtualHostsAsync(cancellationToken);
            Replace(PurgeVirtualHosts, virtualHosts);
            PurgeConnectionInfo = $"{info.ClusterName} / RabbitMQ {info.RabbitMqVersion}";
            IsPurgeConnected = true;
            await RestoreSelectedVirtualHostAsync(PurgeVirtualHosts, RabbitMqClientRole.QueuePurge, value => SelectedPurgeVirtualHost = value);
            await SaveConnectionAsync(RabbitMqClientRole.QueuePurge, SelectedPurgeVirtualHost?.Name, cancellationToken);
            _logService.Success($"RabbitMQ 清理端连接成功：{PurgeConnectionInfo}");
        });
    }

    [RelayCommand]
    private async Task RefreshPurgeQueuesAsync()
    {
        if (_purgeClient == null || SelectedPurgeVirtualHost == null)
        {
            _logService.Warning("请先连接 RabbitMQ 并选择 Virtual Host");
            return;
        }

        await RunBusyAsync("正在刷新 Queue 消息统计...", async cancellationToken =>
        {
            var queues = await new RabbitMqResourceLoader(_purgeClient)
                .LoadPurgeQueuesAsync(SelectedPurgeVirtualHost.Name, cancellationToken);
            Replace(PurgeQueues, queues);
            RefreshPurgeTotals();
            await SaveConnectionAsync(RabbitMqClientRole.QueuePurge, SelectedPurgeVirtualHost.Name, cancellationToken);
            _logService.Info($"Queue 已刷新：{PurgeQueues.Count} 个，Ready {ReadyTotal}，Unacked {UnackedTotal}");
        });
    }

    [RelayCommand]
    private async Task PurgeAllReadyMessagesAsync()
    {
        if (_purgeClient == null || SelectedPurgeVirtualHost == null) return;
        var service = new RabbitMqQueuePurgeService(_purgeClient);
        var preview = await service.BuildPreviewAsync(SelectedPurgeVirtualHost.Name, CancellationToken.None);
        var request = RabbitMqConfirmation.BuildPurge(
            PurgeConnectionInfo,
            PurgeAddress,
            preview.VirtualHost,
            preview.Queues.Count,
            preview.ReadyTotal,
            preview.UnackedTotal);
        if (!await _confirmationService.ConfirmAsync(request)) return;

        await RunBusyAsync("正在清空 Queue Ready 消息...", async cancellationToken =>
        {
            var progress = new Progress<(int Completed, int Total, string Current)>(value =>
                ProgressText = $"{value.Completed}/{value.Total} {value.Current}");
            var summary = await service.ExecuteAsync(preview, progress, cancellationToken);
            PurgeSummary = $"成功 {summary.SucceededCount}｜失败 {summary.FailedCount}｜剩余 Ready {summary.ReadyAfter}｜Unacked {summary.UnackedAfter}";
            _logService.Warning($"Queue 消息清理结束：{PurgeSummary}");
        });
        await RefreshPurgeQueuesAsync();
    }

    [RelayCommand]
    private void Cancel() => _operationCancellation?.Cancel();

    [RelayCommand]
    private void ClearLogs() => Logs.Clear();

    private bool CanExecutePlan() => CurrentPlan != null && !IsBusy;

    partial void OnOperationModeChanged(RabbitMqOperationMode value)
    {
        OnPropertyChanged(nameof(IsTopologyMode));
        OnPropertyChanged(nameof(IsPurgeMode));
    }

    partial void OnCurrentPlanChanged(RabbitMqSyncPlan? value) =>
        ExecutePlanCommand.NotifyCanExecuteChanged();

    partial void OnIsBusyChanged(bool value) => ExecutePlanCommand.NotifyCanExecuteChanged();

    partial void OnSelectionModeChanged(RabbitMqSelectionMode value) => InvalidatePlan();

    partial void OnSelectedSourceVirtualHostChanged(RabbitMqVirtualHost? value)
    {
        TargetVirtualHostName = value?.Name ?? string.Empty;
        _sourceSnapshot = null;
        InvalidatePlan();
        RefreshTargetVirtualHostStatus();
        if (value != null) _ = SaveConnectionAsync(RabbitMqClientRole.SourceReadOnly, value.Name, CancellationToken.None);
    }

    partial void OnSelectedPurgeVirtualHostChanged(RabbitMqVirtualHost? value)
    {
        PurgeQueues.Clear();
        RefreshPurgeTotals();
        if (value != null) _ = SaveConnectionAsync(RabbitMqClientRole.QueuePurge, value.Name, CancellationToken.None);
    }

    private async Task RunBusyAsync(string text, Func<CancellationToken, Task> action)
    {
        if (IsBusy) return;
        _operationCancellation = new CancellationTokenSource();
        IsBusy = true;
        BusyText = text;
        ProgressText = string.Empty;
        try
        {
            await action(_operationCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            _logService.Warning("操作已取消");
        }
        catch (Exception ex)
        {
            _logService.Error(ex.Message);
        }
        finally
        {
            IsBusy = false;
            BusyText = string.Empty;
            _operationCancellation.Dispose();
            _operationCancellation = null;
        }
    }

    private RabbitMqConnectionSettings SourceSettings() =>
        Settings(SourceAddress, SourceUsername, SourcePassword);

    private RabbitMqConnectionSettings TargetSettings() =>
        Settings(TargetAddress, TargetUsername, TargetPassword);

    private RabbitMqConnectionSettings PurgeSettings() =>
        Settings(PurgeAddress, PurgeUsername, PurgePassword);

    private static RabbitMqConnectionSettings Settings(string address, string username, string password)
    {
        if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(username))
            throw new InvalidOperationException("Management API 地址和用户名不能为空");
        return new RabbitMqConnectionSettings(address, username, password, TimeSpan.FromSeconds(15));
    }

    private async Task LoadSavedConnectionAsync(RabbitMqClientRole role)
    {
        var saved = await _settingsStore.LoadAsync(role, CancellationToken.None);
        if (saved == null) return;
        switch (role)
        {
            case RabbitMqClientRole.SourceReadOnly:
                SourceAddress = saved.Address; SourceUsername = saved.Username; SourcePassword = saved.Password;
                break;
            case RabbitMqClientRole.TargetTopology:
                TargetAddress = saved.Address; TargetUsername = saved.Username; TargetPassword = saved.Password;
                break;
            case RabbitMqClientRole.QueuePurge:
                PurgeAddress = saved.Address; PurgeUsername = saved.Username; PurgePassword = saved.Password;
                break;
        }
    }

    private async Task SaveConnectionAsync(
        RabbitMqClientRole role,
        string? virtualHost,
        CancellationToken cancellationToken)
    {
        var connection = role switch
        {
            RabbitMqClientRole.SourceReadOnly => new RabbitMqSavedConnection(SourceAddress, SourceUsername, SourcePassword, 15, true, virtualHost),
            RabbitMqClientRole.TargetTopology => new RabbitMqSavedConnection(TargetAddress, TargetUsername, TargetPassword, 15, true, virtualHost),
            RabbitMqClientRole.QueuePurge => new RabbitMqSavedConnection(PurgeAddress, PurgeUsername, PurgePassword, 15, true, virtualHost),
            _ => throw new ArgumentOutOfRangeException(nameof(role))
        };
        await _settingsStore.SaveAsync(role, connection, cancellationToken);
    }

    private async Task RestoreSelectedVirtualHostAsync(
        IEnumerable<RabbitMqVirtualHost> values,
        RabbitMqClientRole role,
        Action<RabbitMqVirtualHost?> setter)
    {
        var saved = await _settingsStore.LoadAsync(role, CancellationToken.None);
        setter(values.FirstOrDefault(value => string.Equals(value.Name, saved?.SelectedVirtualHost, StringComparison.Ordinal))
               ?? values.FirstOrDefault());
    }

    private void RefreshTargetVirtualHostStatus()
    {
        TargetVirtualHostStatus = string.IsNullOrEmpty(TargetVirtualHostName)
            ? "未选择源 Virtual Host"
            : TargetVirtualHosts.Any(item => string.Equals(item.Name, TargetVirtualHostName, StringComparison.Ordinal))
                ? "目标同名 Virtual Host 已存在"
                : "目标同名 Virtual Host 不存在，将创建";
    }

    private static HashSet<string> SelectedNames<T>(
        IEnumerable<RabbitMqSelectionItem<T>> values,
        RabbitMqSelectionMode mode) =>
        values.Where(item => mode == RabbitMqSelectionMode.WholeVirtualHost || item.IsSelected)
            .Select(item => item.Identity)
            .ToHashSet(StringComparer.Ordinal);

    private void SetSelections<T>(
        ObservableCollection<RabbitMqSelectionItem<T>> target,
        IEnumerable<T> resources,
        Func<T, string> identity)
    {
        target.Clear();
        foreach (var resource in resources)
        {
            var item = new RabbitMqSelectionItem<T>(resource, identity(resource), true);
            item.PropertyChanged += OnSelectionChanged;
            target.Add(item);
        }
    }

    private void OnSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RabbitMqSelectionItem<object>.IsSelected)) InvalidatePlan();
    }

    private void InvalidatePlan()
    {
        CurrentPlan = null;
        PlanSummary = "选择或连接状态已变化，请重新生成同步预览";
    }

    private void RefreshPurgeTotals()
    {
        ReadyTotal = PurgeQueues.Sum(item => item.MessagesReady);
        UnackedTotal = PurgeQueues.Sum(item => item.MessagesUnacknowledged);
        ConsumerTotal = PurgeQueues.Sum(item => item.Consumers);
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values) target.Add(value);
    }

    private void OnLogAdded(RabbitMqLogEntry entry)
    {
        void Add()
        {
            Logs.Add(entry);
            while (Logs.Count > 1000) Logs.RemoveAt(0);
        }

        if (Application.Current?.Dispatcher is { } dispatcher) dispatcher.Invoke(Add);
        else Add();
    }
}
