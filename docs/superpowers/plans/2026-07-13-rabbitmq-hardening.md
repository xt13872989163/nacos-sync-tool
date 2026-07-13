# RabbitMQ Native Module Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复 RabbitMQ 原生 Windows 模块的页面显示、连接状态、执行汇总和 HTTP 安全问题，使拓扑同步与 Queue Ready 清理能够稳定、安全地完成完整用户流程。

**Architecture:** 保留现有 Nacos 代码和 RabbitMQ 服务分层，通过 Shell 绑定修复、ViewModel 连接会话失效机制、可释放 Management Client、安全 URI 构造和准确执行汇总完成加固。所有 RabbitMQ 写操作继续经过显式角色接口；拓扑同步保持无 DELETE，Queue Purge 继续是唯一 DELETE 能力。

**Tech Stack:** C# 14、.NET 10、WPF、CommunityToolkit.Mvvm、HttpClient、xUnit v3、GitHub Actions Windows runner。

---

## 执行约束

- 仅修改 `native/windows/**`、RabbitMQ 相关 README 和本计划状态。
- 不修改根目录 Electron/Vue `src/`。
- 不修改或提交 `artifacts/`。
- 用户明确要求不在本地安装 .NET SDK、不运行本地构建。
- 每个任务先写回归测试，再写实现；测试执行统一由 GitHub Actions 完成。
- 每次推送后检查 `.github/workflows/build-windows-native.yml` 的 Restore、Build、Run tests、x64 Publish、ARM64 Publish。

## 文件结构变化

- Modify: `native/windows/src/NacosSyncTool.Windows/MainWindow.xaml`
  - 修正 RabbitMQ 根视图的 Shell Visibility 绑定。
- Modify: `native/windows/src/NacosSyncTool.Windows/App.xaml`
- Modify: `native/windows/src/NacosSyncTool.Windows/App.xaml.cs`
  - 应用退出时释放 DI 容器和 RabbitMQ 客户端。
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Models/RabbitMqConnection.cs`
  - 增加不可变已连接端点摘要。
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Models/RabbitMqSyncPlan.cs`
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Models/RabbitMqQueuePurgeResult.cs`
  - 增加 Missing、Cancelled、NotProcessed 汇总。
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/IRabbitMqManagementClient.cs`
  - 明确 `IDisposable` 生命周期。
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/RabbitMqManagementClient.cs`
  - 禁用重定向、校验地址与最终 Purge URI。
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/RabbitMqLogService.cs`
  - 完整脱敏 Basic、JSON 和 URL userinfo。
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/RabbitMqSyncExecutor.cs`
  - 重试读取、处理 409 复查、记录未处理项。
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/RabbitMqQueuePurgeService.cs`
  - 准确记录 Missing、Cancelled、NotProcessed。
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/ViewModels/RabbitMqModuleViewModel.cs`
  - 连接失效、候选客户端、计划失效、异常边界和命令状态。
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Views/RabbitMqModuleView.xaml`
  - 只读资源列、连接状态、清理取消和全屏忙碌遮罩。
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Views/RabbitMqModuleView.xaml.cs`
  - PasswordBox 修改后触发连接失效。
- Create: `native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/FakeRabbitMqClientFactory.cs`
  - 为 ViewModel 测试提供可观察、可释放的候选客户端。
- Create: `native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/FakeRabbitMqConfirmationService.cs`
  - 捕获同步和清理确认内容。
- Create: `native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqModuleViewModelTests.cs`
  - 覆盖连接输入变化、旧计划、确认地址、异常和重入。
- Modify existing RabbitMQ tests for Shell、HTTP、日志、执行器、Purge 和 XAML。

---

### Task 1: 修复模块显示和只读界面

**Files:**
- Modify: `native/windows/src/NacosSyncTool.Windows/MainWindow.xaml:644-646`
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Views/RabbitMqModuleView.xaml:143-245`
- Modify: `native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqViewBindingTests.cs`

- [ ] **Step 1: 写 Shell Visibility 和只读列回归测试**

在 `RabbitMqViewBindingTests.cs` 增加：

```csharp
[Fact]
public void MainWindowBindsRabbitVisibilityToShellOnWindow()
{
    var xaml = ReadProjectFile("MainWindow.xaml");

    Assert.Contains("DataContext=\"{Binding RabbitMq}\"", xaml);
    Assert.Contains(
        "Visibility=\"{Binding DataContext.IsRabbitMqSelected, RelativeSource={RelativeSource AncestorType=Window}, Converter={StaticResource BoolToVisibility}}\"",
        xaml);
}

[Fact]
public void ResourceColumnsAreReadOnlyButSelectionColumnsRemainEditable()
{
    var xaml = ReadRabbitView();

    Assert.Contains("Header=\"选择\" Binding=\"{Binding IsSelected}\"", xaml);
    foreach (var header in new[] { "名称", "类型", "Durable", "来源", "目标", "Routing Key", "Pattern", "Priority" })
        Assert.Contains($"Header=\"{header}\"", xaml);
    Assert.True(xaml.Split("IsReadOnly=\"True\"").Length >= 12);
}

private static string ReadProjectFile(string fileName)
{
    var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "NacosSyncTool.Windows", fileName);
    return File.ReadAllText(Path.GetFullPath(path));
}

private static string ReadRabbitView() => ReadProjectFile(
    Path.Combine("Modules", "RabbitMq", "Views", "RabbitMqModuleView.xaml"));
```

- [ ] **Step 2: 静态确认旧代码下测试预期失败**

Run when SDK is available:

```powershell
dotnet test native/windows/NacosSyncTool.Windows.sln --filter FullyQualifiedName~RabbitMqViewBindingTests
```

Expected before implementation: FAIL because RabbitMQ Visibility has no Window RelativeSource and resource columns are editable.

- [ ] **Step 3: 修正 RabbitMQ 根视图绑定**

将 `MainWindow.xaml` RabbitMQ 根视图改为：

```xml
<rabbitViews:RabbitMqModuleView
    DataContext="{Binding RabbitMq}"
    BackToNacosRequested="OnBackToNacosRequested"
    Visibility="{Binding DataContext.IsRabbitMqSelected,
                         RelativeSource={RelativeSource AncestorType=Window},
                         Converter={StaticResource BoolToVisibility}}"/>
```

- [ ] **Step 4: 锁定资源列并增加清理页取消入口**

保持四个“选择”列可编辑；为所有 `Resource.*` 数据列增加 `IsReadOnly="True"`。在清理操作栏增加：

```xml
<Button Content="取消" Margin="8,0,0,0"
        Style="{StaticResource SecondaryButton}"
        Command="{Binding CancelCommand}"/>
```

把忙碌遮罩改为覆盖整个根 Grid 的容器，外层接受鼠标命中，内部面板居中：

```xml
<Grid Grid.RowSpan="4" Background="#66000000" Panel.ZIndex="100"
      Visibility="{Binding IsBusy, Converter={StaticResource BoolToVisibility}}">
  <Border Background="#EE202020" CornerRadius="8" Padding="18"
          HorizontalAlignment="Center" VerticalAlignment="Center">
    <StackPanel>
      <TextBlock Text="{Binding BusyText}" Foreground="White" FontWeight="SemiBold"/>
      <TextBlock Text="{Binding ProgressText}" Foreground="#DDFFFFFF" Margin="0,5,0,0"/>
      <Button Content="取消" Margin="0,12,0,0" Command="{Binding CancelCommand}"/>
    </StackPanel>
  </Border>
</Grid>
```

- [ ] **Step 5: 提交 Task 1**

```powershell
git add native/windows/src/NacosSyncTool.Windows/MainWindow.xaml native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Views/RabbitMqModuleView.xaml native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqViewBindingTests.cs
git commit -m "fix: show RabbitMQ module and lock topology fields"
```

---

### Task 2: 建立可释放的连接会话和 ViewModel 测试基础

**Files:**
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Models/RabbitMqConnection.cs`
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/IRabbitMqManagementClient.cs`
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/ViewModels/RabbitMqModuleViewModel.cs`
- Modify: `native/windows/src/NacosSyncTool.Windows/App.xaml`
- Modify: `native/windows/src/NacosSyncTool.Windows/App.xaml.cs`
- Create: `native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/FakeRabbitMqClientFactory.cs`
- Create: `native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/FakeRabbitMqConfirmationService.cs`
- Create: `native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqModuleViewModelTests.cs`

- [ ] **Step 1: 增加已连接端点和释放契约**

在 `RabbitMqConnection.cs` 增加：

```csharp
public sealed record RabbitMqConnectedEndpoint(
    string Address,
    string ClusterName,
    string RabbitMqVersion)
{
    public string DisplayName => $"{ClusterName} / RabbitMQ {RabbitMqVersion}";
}
```

只修改客户端接口声明，使现有方法集合获得释放契约：

```csharp
public interface IRabbitMqManagementClient : IDisposable
```

测试 Fake Client 增加 `Disposed` 和 `Dispose()`：

```csharp
public bool Disposed { get; private set; }
public void Dispose() => Disposed = true;
```

- [ ] **Step 2: 创建可排队的 Fake Factory 和确认服务**

`FakeRabbitMqClientFactory.cs`：

```csharp
internal sealed class FakeRabbitMqClientFactory : IRabbitMqClientFactory
{
    public Queue<FakeRabbitMqManagementClient> Clients { get; } = new();
    public List<(RabbitMqConnectionSettings Settings, RabbitMqClientRole Role)> Requests { get; } = [];

    public IRabbitMqManagementClient Create(RabbitMqConnectionSettings settings, RabbitMqClientRole role)
    {
        Requests.Add((settings, role));
        return Clients.Dequeue();
    }
}
```

`FakeRabbitMqConfirmationService.cs`：

```csharp
internal sealed class FakeRabbitMqConfirmationService : IRabbitMqConfirmationService
{
    public bool Result { get; set; } = true;
    public RabbitMqConfirmationRequest? LastRequest { get; private set; }

    public Task<bool> ConfirmAsync(RabbitMqConfirmationRequest request)
    {
        LastRequest = request;
        return Task.FromResult(Result);
    }
}
```

在 `RabbitMqModuleViewModelTests.cs` 增加完整测试辅助方法：

```csharp
private static RabbitMqModuleViewModel CreateViewModel(
    FakeRabbitMqClientFactory factory,
    FakeRabbitMqConfirmationService? confirmation = null,
    RabbitMqSettingsStore? settingsStore = null)
{
    return new RabbitMqModuleViewModel(
        factory,
        new RabbitMqPlanBuilder(),
        settingsStore ?? new RabbitMqSettingsStore(NewSettingsPath()),
        new RabbitMqLogService(),
        confirmation ?? new FakeRabbitMqConfirmationService());
}

private static FakeRabbitMqManagementClient ConnectedClient(
    string cluster,
    params RabbitMqQueue[] queues) =>
    new()
    {
        ConnectionInfo = new RabbitMqConnectionInfo("4.1.0", cluster),
        VirtualHosts = [new RabbitMqVirtualHost("/", null, [], null)],
        Queues = queues
    };

private static RabbitMqSyncPlan PlanWithPendingQueue(string name) =>
    new(
        "/",
        DateTimeOffset.UtcNow,
        [new RabbitMqSyncPlanItem(
            RabbitMqResourceType.Queue,
            name,
            Queue(name, 0, 0),
            RabbitMqPlanStatus.PendingCreate,
            "目标同名资源不存在，将创建")]);

private static RabbitMqQueue Queue(string name, long ready, long unacked) =>
    new(name, true, false, false, [], ready, unacked, 0);

private static string NewSettingsPath() => Path.Combine(
    Path.GetTempPath(),
    "nacos-sync-tool-tests",
    Guid.NewGuid().ToString("N"),
    "rabbitmq-settings.json");
```

- [ ] **Step 3: 写连接信息变化失效测试**

在 `RabbitMqModuleViewModelTests.cs` 增加测试夹具，并写：

```csharp
[Fact]
public async Task ChangingTargetAddressDisposesClientAndInvalidatesPlan()
{
    var first = ConnectedClient("target-a");
    var factory = new FakeRabbitMqClientFactory();
    factory.Clients.Enqueue(first);
    var vm = CreateViewModel(factory);
    vm.TargetAddress = "http://target-a:15672";
    vm.TargetUsername = "guest";

    await vm.TestTargetConnectionCommand.ExecuteAsync(null);
    vm.CurrentPlan = PlanWithPendingQueue("orders");

    vm.TargetAddress = "http://target-b:15672";

    Assert.True(first.Disposed);
    Assert.False(vm.IsTargetConnected);
    Assert.Null(vm.CurrentPlan);
    Assert.Empty(vm.TargetVirtualHosts);
}

[Fact]
public async Task FailedReconnectLeavesRoleDisconnectedAndDisposesCandidate()
{
    var candidate = ConnectedClient("target-a");
    candidate.FailureForCall = call => call == "TestConnection"
        ? new HttpRequestException("offline")
        : null;
    var factory = new FakeRabbitMqClientFactory();
    factory.Clients.Enqueue(candidate);
    var vm = CreateViewModel(factory);

    await vm.TestTargetConnectionCommand.ExecuteAsync(null);

    Assert.True(candidate.Disposed);
    Assert.False(vm.IsTargetConnected);
    Assert.Null(vm.CurrentPlan);
}
```

- [ ] **Step 4: 实现角色失效方法和候选连接交换**

`RabbitMqModuleViewModel` 实现 `IDisposable`，增加：

```csharp
private RabbitMqConnectedEndpoint? _sourceEndpoint;
private RabbitMqConnectedEndpoint? _targetEndpoint;
private RabbitMqConnectedEndpoint? _purgeEndpoint;
private bool _isInitializing;

private void InvalidateTargetConnection()
{
    _targetClient?.Dispose();
    _targetClient = null;
    _targetEndpoint = null;
    IsTargetConnected = false;
    TargetConnectionInfo = string.Empty;
    TargetVirtualHosts.Clear();
    InvalidatePlan();
    RefreshTargetVirtualHostStatus();
}

partial void OnTargetAddressChanged(string value)
{
    if (!_isInitializing) InvalidateTargetConnection();
}

partial void OnTargetUsernameChanged(string value)
{
    if (!_isInitializing) InvalidateTargetConnection();
}

partial void OnTargetPasswordChanged(string value)
{
    if (!_isInitializing) InvalidateTargetConnection();
}
```

同时实现 Source 和 QueuePurge 的明确失效方法：

```csharp
private void InvalidateSourceConnection()
{
    _sourceClient?.Dispose();
    _sourceClient = null;
    _sourceEndpoint = null;
    IsSourceConnected = false;
    SourceConnectionInfo = string.Empty;
    SourceVirtualHosts.Clear();
    SelectedSourceVirtualHost = null;
    _sourceSnapshot = null;
    Exchanges.Clear();
    Queues.Clear();
    Bindings.Clear();
    Policies.Clear();
    InvalidatePlan();
}

private void InvalidatePurgeConnection()
{
    _purgeClient?.Dispose();
    _purgeClient = null;
    _purgeEndpoint = null;
    IsPurgeConnected = false;
    PurgeConnectionInfo = string.Empty;
    PurgeVirtualHosts.Clear();
    SelectedPurgeVirtualHost = null;
    PurgeQueues.Clear();
    PurgeSummary = string.Empty;
    RefreshPurgeTotals();
}

partial void OnSourceAddressChanged(string value) { if (!_isInitializing) InvalidateSourceConnection(); }
partial void OnSourceUsernameChanged(string value) { if (!_isInitializing) InvalidateSourceConnection(); }
partial void OnSourcePasswordChanged(string value) { if (!_isInitializing) InvalidateSourceConnection(); }
partial void OnPurgeAddressChanged(string value) { if (!_isInitializing) InvalidatePurgeConnection(); }
partial void OnPurgeUsernameChanged(string value) { if (!_isInitializing) InvalidatePurgeConnection(); }
partial void OnPurgePasswordChanged(string value) { if (!_isInitializing) InvalidatePurgeConnection(); }
```

连接命令使用候选客户端：

```csharp
private async Task TestTargetConnectionAsync()
{
    InvalidateTargetConnection();
    await RunBusyAsync("正在连接目标 RabbitMQ...", async cancellationToken =>
    {
        IRabbitMqManagementClient? candidate = _clientFactory.Create(
            TargetSettings(), RabbitMqClientRole.TargetTopology);
        try
        {
            var info = await candidate.TestConnectionAsync(cancellationToken);
            var virtualHosts = await candidate.GetVirtualHostsAsync(cancellationToken);
            _targetClient = candidate;
            candidate = null;
            _targetEndpoint = new RabbitMqConnectedEndpoint(
                TargetAddress.Trim(), info.ClusterName, info.RabbitMqVersion);
            Replace(TargetVirtualHosts, virtualHosts);
            TargetConnectionInfo = _targetEndpoint.DisplayName;
            IsTargetConnected = true;
            RefreshTargetVirtualHostStatus();
            await SaveConnectionAsync(
                RabbitMqClientRole.TargetTopology,
                TargetVirtualHostName,
                cancellationToken);
        }
        finally
        {
            candidate?.Dispose();
        }
    });
}
```

Source 和 QueuePurge 连接命令分别使用以下赋值规则：

```csharp
// Source 成功后
_sourceClient = candidate;
candidate = null;
_sourceEndpoint = new RabbitMqConnectedEndpoint(
    SourceAddress.Trim(), info.ClusterName, info.RabbitMqVersion);
Replace(SourceVirtualHosts, virtualHosts);
SourceConnectionInfo = _sourceEndpoint.DisplayName;
IsSourceConnected = true;
await RestoreSelectedVirtualHostAsync(
    SourceVirtualHosts,
    RabbitMqClientRole.SourceReadOnly,
    value => SelectedSourceVirtualHost = value);

// QueuePurge 成功后
_purgeClient = candidate;
candidate = null;
_purgeEndpoint = new RabbitMqConnectedEndpoint(
    PurgeAddress.Trim(), info.ClusterName, info.RabbitMqVersion);
Replace(PurgeVirtualHosts, virtualHosts);
PurgeConnectionInfo = _purgeEndpoint.DisplayName;
IsPurgeConnected = true;
await RestoreSelectedVirtualHostAsync(
    PurgeVirtualHosts,
    RabbitMqClientRole.QueuePurge,
    value => SelectedPurgeVirtualHost = value);
```

Source 命令在进入 `RunBusyAsync` 前调用 `InvalidateSourceConnection()`，QueuePurge 命令调用 `InvalidatePurgeConnection()`；候选客户端均使用 `try/finally`，未成功转移给字段时在 `finally` 中 Dispose。

- [ ] **Step 5: 应用退出时释放服务容器**

`App.xaml` 增加：

```xml
Exit="OnExit"
```

`App.xaml.cs`：

```csharp
private void OnExit(object sender, ExitEventArgs e)
{
    if (ServiceProvider is IDisposable disposable) disposable.Dispose();
}
```

ViewModel `Dispose()` 释放三个客户端并取消订阅日志事件。

- [ ] **Step 6: 提交 Task 2**

```powershell
git add native/windows/src/NacosSyncTool.Windows/App.xaml native/windows/src/NacosSyncTool.Windows/App.xaml.cs native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Models/RabbitMqConnection.cs native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/IRabbitMqManagementClient.cs native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/ViewModels/RabbitMqModuleViewModel.cs native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/FakeRabbitMqManagementClient.cs native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/FakeRabbitMqClientFactory.cs native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/FakeRabbitMqConfirmationService.cs native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqModuleViewModelTests.cs
git commit -m "fix: invalidate and dispose RabbitMQ connections"
```

---

### Task 3: 收紧命令状态、异常边界和清理确认

**Files:**
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/ViewModels/RabbitMqModuleViewModel.cs`
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Views/RabbitMqModuleView.xaml`
- Modify: `native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqModuleViewModelTests.cs`

- [ ] **Step 1: 写确认使用已连接地址和预览失败测试**

```csharp
[Fact]
public async Task PurgeConfirmationUsesConnectedEndpointAndInputChangeBlocksPurge()
{
    var purge = ConnectedClient("cluster-a", Queue("orders", 5, 2));
    var confirmation = new FakeRabbitMqConfirmationService();
    var factory = new FakeRabbitMqClientFactory();
    factory.Clients.Enqueue(purge);
    var vm = CreateViewModel(factory, confirmation);
    vm.PurgeAddress = "http://cluster-a:15672";
    vm.PurgeUsername = "guest";
    await vm.TestPurgeConnectionCommand.ExecuteAsync(null);

    vm.PurgeAddress = "http://cluster-b:15672";
    await vm.PurgeAllReadyMessagesCommand.ExecuteAsync(null);

    Assert.Null(confirmation.LastRequest);
    Assert.DoesNotContain(purge.Calls, call => call.StartsWith("PurgeQueue", StringComparison.Ordinal));
}

[Fact]
public async Task PreviewFailureIsLoggedAndDoesNotPurge()
{
    var purge = ConnectedClient("cluster-a");
    purge.FailureForCall = call => call.StartsWith("GetQueues", StringComparison.Ordinal)
        ? new HttpRequestException("offline")
        : null;
    var factory = new FakeRabbitMqClientFactory();
    factory.Clients.Enqueue(purge);
    var vm = CreateViewModel(factory);
    vm.PurgeAddress = "http://cluster-a:15672";
    vm.PurgeUsername = "guest";
    await vm.TestPurgeConnectionCommand.ExecuteAsync(null);

    await vm.PurgeAllReadyMessagesCommand.ExecuteAsync(null);

    Assert.False(vm.IsBusy);
    Assert.Contains(vm.Logs, entry => entry.Level == LogLevel.Error && entry.Message.Contains("offline"));
    Assert.DoesNotContain(purge.Calls, call => call.StartsWith("PurgeQueue", StringComparison.Ordinal));
}
```

- [ ] **Step 2: 让 RunBusy 返回执行状态**

```csharp
private async Task<bool> RunBusyAsync(
    string text,
    Func<CancellationToken, Task> action)
{
    if (IsBusy) return false;
    _operationCancellation = new CancellationTokenSource();
    IsBusy = true;
    BusyText = text;
    ProgressText = string.Empty;
    try
    {
        await action(_operationCancellation.Token);
        return true;
    }
    catch (OperationCanceledException)
    {
        _logService.Warning("操作已取消");
        return false;
    }
    catch (Exception ex)
    {
        _logService.Error(ex.Message);
        return false;
    }
    finally
    {
        IsBusy = false;
        BusyText = string.Empty;
        _operationCancellation.Dispose();
        _operationCancellation = null;
    }
}
```

- [ ] **Step 3: 把 Purge 预览纳入异常和取消范围**

```csharp
private async Task PurgeAllReadyMessagesAsync()
{
    if (_purgeClient == null || _purgeEndpoint == null || SelectedPurgeVirtualHost == null)
    {
        _logService.Warning("请使用当前输入重新连接 RabbitMQ 清理端");
        return;
    }

    RabbitMqQueuePurgePreview? preview = null;
    var previewLoaded = await RunBusyAsync(
        "正在读取 Queue 消息统计...",
        async cancellationToken =>
        {
            preview = await new RabbitMqQueuePurgeService(_purgeClient)
                .BuildPreviewAsync(SelectedPurgeVirtualHost.Name, cancellationToken);
        });
    if (!previewLoaded || preview == null) return;

    var request = RabbitMqConfirmation.BuildPurge(
        _purgeEndpoint.DisplayName,
        _purgeEndpoint.Address,
        preview.VirtualHost,
        preview.Queues.Count,
        preview.ReadyTotal,
        preview.UnackedTotal);
    if (!await _confirmationService.ConfirmAsync(request)) return;

    await RunBusyAsync("正在清空 Queue Ready 消息...", cancellationToken =>
        ExecutePurgeAsync(preview, cancellationToken));
    await RefreshPurgeQueuesAsync();
}
```

`ExecutePurgeAsync` 实现为：

```csharp
private async Task ExecutePurgeAsync(
    RabbitMqQueuePurgePreview preview,
    CancellationToken cancellationToken)
{
    if (_purgeClient == null) return;
    var progress = new Progress<(int Completed, int Total, string Current)>(value =>
        ProgressText = $"{value.Completed}/{value.Total} {value.Current}");
    var summary = await new RabbitMqQueuePurgeService(_purgeClient)
        .ExecuteAsync(preview, progress, cancellationToken);
    var remaining = summary.StatisticsRefreshed
        ? $"剩余 Ready {summary.ReadyAfter}｜Unacked {summary.UnackedAfter}"
        : "剩余消息统计刷新失败，请手动刷新确认";
    PurgeSummary =
        $"成功 {summary.SucceededCount}｜失败 {summary.FailedCount}｜缺失 {summary.MissingCount}｜取消/未处理 {summary.CancelledCount + summary.NotProcessedCount}｜{remaining}";
    if (summary.HasProblems)
        _logService.Warning($"Queue 消息清理未完整完成：{PurgeSummary}");
    else
        _logService.Success($"Queue 消息清理结束：{PurgeSummary}");
}
```

- [ ] **Step 4: 为所有命令增加 CanExecute 并统一通知**

```csharp
private bool CanStartOperation() => !IsBusy;
private bool CanLoadSource() => !IsBusy && _sourceClient != null && SelectedSourceVirtualHost != null;
private bool CanBuildPlan() => !IsBusy && _sourceSnapshot != null && _targetClient != null;
private bool CanExecutePlan() => !IsBusy && CurrentPlan != null && _targetClient != null;
private bool CanPurge() => !IsBusy && _purgeClient != null && _purgeEndpoint != null && SelectedPurgeVirtualHost != null;
private bool CanCancel() => IsBusy;

[RelayCommand(CanExecute = nameof(CanCancel))]
private void Cancel() => _operationCancellation?.Cancel();
```

把现有命令的 `[RelayCommand]` 属性按以下映射修改，不改变各步骤已经给出的函数体：

| 方法 | CanExecute |
|---|---|
| `TestSourceConnectionAsync` | `CanStartOperation` |
| `TestTargetConnectionAsync` | `CanStartOperation` |
| `RefreshResourcesAsync` | `CanLoadSource` |
| `BuildPlanAsync` | `CanBuildPlan` |
| `ExecutePlanAsync` | `CanExecutePlan` |
| `RecheckAndExecuteAsync` | `CanBuildPlan` |
| `TestPurgeConnectionAsync` | `CanStartOperation` |
| `RefreshPurgeQueuesAsync` | `CanPurge` |
| `PurgeAllReadyMessagesAsync` | `CanPurge` |

`OnIsBusyChanged` 调用以下通知：

```csharp
TestSourceConnectionCommand.NotifyCanExecuteChanged();
TestTargetConnectionCommand.NotifyCanExecuteChanged();
RefreshResourcesCommand.NotifyCanExecuteChanged();
BuildPlanCommand.NotifyCanExecuteChanged();
ExecutePlanCommand.NotifyCanExecuteChanged();
TestPurgeConnectionCommand.NotifyCanExecuteChanged();
RefreshPurgeQueuesCommand.NotifyCanExecuteChanged();
PurgeAllReadyMessagesCommand.NotifyCanExecuteChanged();
RecheckAndExecuteCommand.NotifyCanExecuteChanged();
CancelCommand.NotifyCanExecuteChanged();
```

- [ ] **Step 5: 安全加载损坏设置**

```csharp
private async Task LoadSavedConnectionSafelyAsync(RabbitMqClientRole role)
{
    try
    {
        await LoadSavedConnectionAsync(role);
    }
    catch (Exception ex) when (ex is JsonException or FormatException or CryptographicException or IOException)
    {
        _logService.Warning($"RabbitMQ {role} 保存配置无法读取，已忽略：{ex.GetType().Name}");
    }
}
```

`InitializeAsync` 使用 `_isInitializing = true/false` 包围三个安全加载调用，避免加载过程触发连接失效日志。

- [ ] **Step 6: 提交 Task 3**

```powershell
git add native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/ViewModels/RabbitMqModuleViewModel.cs native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Views/RabbitMqModuleView.xaml native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqModuleViewModelTests.cs
git commit -m "fix: guard RabbitMQ commands and purge confirmation"
```

---

### Task 4: 加固 Management API URI、重定向和日志脱敏

**Files:**
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/RabbitMqManagementClient.cs`
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/RabbitMqLogService.cs`
- Modify: `native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqManagementClientSecurityTests.cs`
- Modify: `native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqConfirmationTests.cs`

- [ ] **Step 1: 写 URI 和脱敏回归测试**

```csharp
[Theory]
[InlineData("https://user:secret@mq.example/")]
[InlineData("https://mq.example/rabbit?token=secret")]
[InlineData("https://mq.example/rabbit#admin")]
public void RejectsAmbiguousOrCredentialBearingBaseAddress(string address)
{
    Assert.Throws<ArgumentException>(() => new RabbitMqManagementClient(
        Settings(address), RabbitMqClientRole.SourceReadOnly, new RecordingHttpMessageHandler(HttpStatusCode.OK, "{}")));
}

[Theory]
[InlineData(".")]
[InlineData("..")]
public async Task PurgeRejectsDotSegmentNamesWithoutSendingRequest(string queueName)
{
    var handler = new RecordingHttpMessageHandler(HttpStatusCode.NoContent, "");
    using var client = new RabbitMqManagementClient(Settings(), RabbitMqClientRole.QueuePurge, handler);

    await Assert.ThrowsAsync<InvalidOperationException>(() =>
        client.PurgeQueueAsync("/", queueName, CancellationToken.None));
    Assert.Empty(handler.Requests);
}

[Fact]
public void LogSanitizerRemovesStandardBasicJsonAndUrlCredentials()
{
    var sanitized = RabbitMqLogService.Sanitize(
        "Authorization: Basic dXNlcjpwYXNz {\"password\":\"secret\"} https://user:pass@mq/");

    Assert.DoesNotContain("dXNlcjpwYXNz", sanitized);
    Assert.DoesNotContain("secret", sanitized);
    Assert.DoesNotContain("user:pass", sanitized);
}
```

- [ ] **Step 2: 拒绝不安全基础地址并正确保留反向代理路径**

```csharp
private static Uri NormalizeBaseAddress(string address)
{
    if (!Uri.TryCreate(address?.Trim(), UriKind.Absolute, out var uri) ||
        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
        !string.IsNullOrEmpty(uri.UserInfo) ||
        !string.IsNullOrEmpty(uri.Query) ||
        !string.IsNullOrEmpty(uri.Fragment))
    {
        throw new ArgumentException(
            "RabbitMQ Management API address must be an HTTP/HTTPS base URL without credentials, query, or fragment.",
            nameof(address));
    }

    var builder = new UriBuilder(uri);
    builder.Path = builder.Path.EndsWith("/", StringComparison.Ordinal)
        ? builder.Path
        : builder.Path + "/";
    return builder.Uri;
}
```

- [ ] **Step 3: 禁用自动重定向并验证最终 Purge URI**

`CreateHandler`：

```csharp
var handler = new HttpClientHandler
{
    AllowAutoRedirect = false
};
```

Purge 构造：

```csharp
private static void RejectDotSegment(string value, string parameterName)
{
    if (value is "." or "..")
        throw new InvalidOperationException($"RabbitMQ {parameterName} cannot be a URI dot segment.");
}

public Task PurgeQueueAsync(string virtualHost, string queueName, CancellationToken cancellationToken)
{
    RejectDotSegment(virtualHost, nameof(virtualHost));
    RejectDotSegment(queueName, nameof(queueName));
    var relativePath = $"api/queues/{Segment(virtualHost)}/{Segment(queueName)}/contents";
    var absoluteUri = new Uri(_httpClient.BaseAddress!, relativePath);
    return SendPurgeAsync(absoluteUri, cancellationToken);
}

private async Task SendPurgeAsync(Uri uri, CancellationToken cancellationToken)
{
    var escapedPath = uri.GetComponents(UriComponents.Path, UriFormat.UriEscaped);
    if (_role != RabbitMqClientRole.QueuePurge ||
        !PurgePathPattern.IsMatch(escapedPath) ||
        !string.IsNullOrEmpty(uri.Query) ||
        !string.IsNullOrEmpty(uri.Fragment))
        throw new InvalidOperationException("Only Queue /contents purge is allowed.");

    using var request = new HttpRequestMessage(HttpMethod.Delete, uri);
    using var response = await _httpClient.SendAsync(request, cancellationToken);
    await EnsureSuccessAsync(response, escapedPath, cancellationToken);
}
```

将正则调整为允许反向代理前缀但要求结尾严格匹配：

```csharp
@"(?:^|/)api/queues/[^/]+/[^/]+/contents$"
```

- [ ] **Step 4: 完整脱敏敏感数据**

在 `RabbitMqLogService` 使用三类预编译正则，并按顺序替换：

```csharp
private static readonly Regex AuthorizationPattern = new(
    @"(?i)\bauthorization\s*[:=]\s*(?:basic\s+)?[^\s,;\}\]]+",
    RegexOptions.Compiled | RegexOptions.CultureInvariant);
private static readonly Regex JsonSensitivePattern = new(
    "(?i)([\"'](?:password|passwd|authorization)[\"']\\s*:\\s*[\"'])[^\"']*([\"'])",
    RegexOptions.Compiled | RegexOptions.CultureInvariant);
private static readonly Regex KeyValueSensitivePattern = new(
    @"(?i)\b(password|passwd)\s*[:=]\s*[^\s,;]+",
    RegexOptions.Compiled | RegexOptions.CultureInvariant);
private static readonly Regex UrlUserInfoPattern = new(
    @"(?i)(https?://)[^/@\s]+@",
    RegexOptions.Compiled | RegexOptions.CultureInvariant);

public static string Sanitize(string message)
{
    var value = message ?? string.Empty;
    value = AuthorizationPattern.Replace(value, "Authorization=***");
    value = JsonSensitivePattern.Replace(value, "$1***$2");
    value = KeyValueSensitivePattern.Replace(value, "$1=***");
    return UrlUserInfoPattern.Replace(value, "$1***@");
}
```

- [ ] **Step 5: 提交 Task 4**

```powershell
git add native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/RabbitMqManagementClient.cs native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/RabbitMqLogService.cs native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqManagementClientSecurityTests.cs native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqConfirmationTests.cs
git commit -m "fix: harden RabbitMQ purge URI and credential logs"
```

---

### Task 5: 修正执行器重试、并发冲突和结果汇总

**Files:**
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Models/RabbitMqSyncPlan.cs`
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Models/RabbitMqQueuePurgeResult.cs`
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/RabbitMqSyncExecutor.cs`
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/RabbitMqQueuePurgeService.cs`
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/ViewModels/RabbitMqModuleViewModel.cs`
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Views/RabbitMqModuleView.xaml`
- Modify: `native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqSyncExecutorTests.cs`
- Modify: `native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqQueuePurgeServiceTests.cs`

- [ ] **Step 1: 扩展执行状态和汇总测试**

增加枚举值：

```csharp
NotProcessed
```

测试：

```csharp
[Fact]
public async Task RetriesTransientExistenceCheck()
{
    var attempts = 0;
    var client = new FakeRabbitMqManagementClient
    {
        Queues = [],
        FailureForCall = call =>
            call.StartsWith("GetQueues", StringComparison.Ordinal) && ++attempts < 3
                ? new HttpRequestException("temporary")
                : null
    };
    var result = await new RabbitMqSyncExecutor(client)
        .ExecuteAsync(PendingQueuePlan("orders"), null, CancellationToken.None);

    Assert.Equal(3, client.Calls.Count(call => call.StartsWith("GetQueues", StringComparison.Ordinal)));
    Assert.Equal(1, result.CreatedCount);
}

[Fact]
public async Task ConflictBecomesSkippedWhenConcurrentResourceExists()
{
    var conflicted = false;
    var client = new FakeRabbitMqManagementClient { Queues = [] };
    client.FailureForCall = call =>
    {
        if (!call.StartsWith("CreateQueue", StringComparison.Ordinal) || conflicted) return null;
        conflicted = true;
        client.Queues = [Queue("orders")];
        return new RabbitMqApiException(HttpStatusCode.Conflict, "queue", "concurrent create");
    };
    var result = await new RabbitMqSyncExecutor(client)
        .ExecuteAsync(PendingQueuePlan("orders"), null, CancellationToken.None);

    Assert.Equal(1, result.SkippedCount);
    Assert.Equal(0, result.FailedCount);
}

[Fact]
public async Task CancellationMarksRemainingItemsAndSummary()
{
    using var cancellation = new CancellationTokenSource();
    var cancelled = false;
    var client = new FakeRabbitMqManagementClient { Queues = [] };
    client.FailureForCall = call =>
    {
        if (!call.StartsWith("CreateQueue", StringComparison.Ordinal) || cancelled) return null;
        cancelled = true;
        cancellation.Cancel();
        return null;
    };
    var result = await new RabbitMqSyncExecutor(client)
        .ExecuteAsync(PendingQueuePlan("one", "two"), null, cancellation.Token);

    Assert.True(result.CancelledCount > 0);
    Assert.Equal(2, result.ProcessedOrAccountedCount);
}

private static RabbitMqSyncPlan PendingQueuePlan(params string[] names) =>
    new(
        "/",
        DateTimeOffset.UtcNow,
        names.Select(name => new RabbitMqSyncPlanItem(
                RabbitMqResourceType.Queue,
                name,
                Queue(name),
                RabbitMqPlanStatus.PendingCreate,
                "目标同名资源不存在，将创建"))
            .ToList());

private static RabbitMqQueue Queue(string name) =>
    new(name, true, false, false, [], 0, 0, 0);
```

- [ ] **Step 2: 扩展汇总属性**

`RabbitMqExecutionSummary`：

```csharp
public int MissingCount => Items.Count(item => item.Status == RabbitMqExecutionStatus.Missing);
public int CancelledCount => Items.Count(item => item.Status == RabbitMqExecutionStatus.Cancelled);
public int NotProcessedCount => Items.Count(item => item.Status == RabbitMqExecutionStatus.NotProcessed);
public int ProcessedOrAccountedCount => Items.Count;
public bool HasProblems => FailedCount + MissingCount + CancelledCount + NotProcessedCount > 0;
```

Purge Summary 增加同名计数和 `HasProblems`。

- [ ] **Step 3: 对存在性检查应用通用重试**

```csharp
private static async Task<T> ExecuteWithRetryAsync<T>(
    Func<Task<T>> action,
    CancellationToken cancellationToken)
{
    for (var attempt = 1; ; attempt++)
    {
        try { return await action(); }
        catch (Exception ex) when (attempt < 3 && IsTransient(ex, cancellationToken))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), cancellationToken);
        }
    }
}
```

执行前复查改为：

```csharp
if (await ExecuteWithRetryAsync(
        () => ExistsAsync(plan.VirtualHost, item, cancellationToken),
        cancellationToken))
{
    results.Add(Result(item, RabbitMqExecutionStatus.Skipped, "执行前复查发现目标资源已存在"));
    continue;
}
```

- [ ] **Step 4: 处理并发创建冲突和未处理项**

Create 抛出 `RabbitMqApiException` 且状态为 Conflict 时，再次调用带重试的 `ExistsAsync`；存在则记录 Skipped，否则记录 Failed。

取消或 401/403 中断时，为当前计划后续所有项增加 `Cancelled` 或 `NotProcessed` 结果，确保汇总覆盖完整计划。

- [ ] **Step 5: 修正 ViewModel 摘要、日志与按钮命名**

```csharp
ExecutionSummary =
    $"创建 {summary.CreatedCount}｜跳过 {summary.SkippedCount}｜失败 {summary.FailedCount}｜缺失 {summary.MissingCount}｜取消/未处理 {summary.CancelledCount + summary.NotProcessedCount}";
if (summary.HasProblems)
    _logService.Warning($"RabbitMQ 同步未完整完成：{ExecutionSummary}");
else
    _logService.Success($"RabbitMQ 同步结束：{ExecutionSummary}");
```

将 `RetryFailedAsync`/`RetryFailedCommand` 重命名为 `RecheckAndExecuteAsync`/`RecheckAndExecuteCommand`，XAML 文案改为“重新检查并执行”。方法开始时必须 `CurrentPlan = null`，重新生成失败时不得执行。

- [ ] **Step 6: 提交 Task 5**

```powershell
git add native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Models/RabbitMqSyncPlan.cs native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Models/RabbitMqQueuePurgeResult.cs native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/RabbitMqSyncExecutor.cs native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/RabbitMqQueuePurgeService.cs native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/ViewModels/RabbitMqModuleViewModel.cs native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Views/RabbitMqModuleView.xaml native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqSyncExecutorTests.cs native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqQueuePurgeServiceTests.cs native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqViewBindingTests.cs
git commit -m "fix: report complete RabbitMQ execution outcomes"
```

---

### Task 6: 明确保存密码状态并完善设置恢复测试

**Files:**
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/ViewModels/RabbitMqModuleViewModel.cs`
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Views/RabbitMqModuleView.xaml`
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Views/RabbitMqModuleView.xaml.cs`
- Modify: `native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqModuleViewModelTests.cs`
- Modify: `native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqSettingsStoreTests.cs`

- [ ] **Step 1: 写密码状态和损坏角色恢复测试**

```csharp
[Fact]
public async Task LoadedPasswordIsMarkedSavedWithoutBeingExposed()
{
    var path = NewSettingsPath();
    var store = new RabbitMqSettingsStore(path);
    await store.SaveAsync(
        RabbitMqClientRole.SourceReadOnly,
        new RabbitMqSavedConnection("http://source:15672", "guest", "secret", 15, true, "/"),
        CancellationToken.None);
    using var vm = CreateViewModel(new FakeRabbitMqClientFactory(), settingsStore: store);

    await vm.InitializeCommand.ExecuteAsync(null);

    Assert.True(vm.HasSavedSourcePassword);
    Assert.Equal("secret", vm.SourcePassword);
    Assert.DoesNotContain("secret", vm.Logs.Select(item => item.Message));
}

[Fact]
public async Task InvalidPurgePasswordDoesNotBlockValidSourceSettings()
{
    var path = NewSettingsPath();
    var store = new RabbitMqSettingsStore(path);
    await store.SaveAsync(
        RabbitMqClientRole.SourceReadOnly,
        new RabbitMqSavedConnection("http://source:15672", "guest", "source-secret", 15, true, "/"),
        CancellationToken.None);
    var root = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
    var connections = root["Connections"]!.AsObject();
    var invalidPurge = connections["SourceReadOnly"]!.DeepClone().AsObject();
    invalidPurge["Address"] = "http://purge:15672";
    invalidPurge["EncryptedPassword"] = "not-base64";
    connections["QueuePurge"] = invalidPurge;
    await File.WriteAllTextAsync(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    using var vm = CreateViewModel(new FakeRabbitMqClientFactory(), settingsStore: store);

    await vm.InitializeCommand.ExecuteAsync(null);

    Assert.Equal("http://source:15672", vm.SourceAddress);
    Assert.Contains(vm.Logs, item => item.Level == LogLevel.Warning);
}
```

- [ ] **Step 2: 增加保存密码状态属性**

```csharp
[ObservableProperty] private bool _hasSavedSourcePassword;
[ObservableProperty] private bool _hasSavedTargetPassword;
[ObservableProperty] private bool _hasSavedPurgePassword;
```

成功加载保存配置后设置对应属性；PasswordBox 发生任何用户输入时设为 false，并通过已有属性变化钩子使连接失效。

- [ ] **Step 3: 在 UI 明确显示密码来源**

每个 PasswordBox 下增加：

```xml
<TextBlock Text="已加载 Windows 加密保存的密码"
           FontSize="10" Foreground="{DynamicResource FaintBrush}"
           Visibility="{Binding HasSavedSourcePassword, Converter={StaticResource BoolToVisibility}}"/>
```

目标端和清理端使用各自属性；不把明文密码绑定回 PasswordBox。

- [ ] **Step 4: 提交 Task 6**

```powershell
git add native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/ViewModels/RabbitMqModuleViewModel.cs native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Views/RabbitMqModuleView.xaml native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Views/RabbitMqModuleView.xaml.cs native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqModuleViewModelTests.cs native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqSettingsStoreTests.cs
git commit -m "fix: surface encrypted RabbitMQ password state"
```

---

### Task 7: 文档、全量审计和 GitHub Actions 验证

**Files:**
- Modify: `README.md`
- Modify: `native/windows/README.md`
- Modify: `docs/superpowers/plans/2026-07-13-rabbitmq-hardening.md`

- [ ] **Step 1: 更新使用说明**

补充以下内容：

- 修改连接地址、用户名或密码后必须重新连接。
- 清空确认展示的地址是实际已验证端点。
- HTTP 3xx 不跟随，包含 userinfo/query/fragment 的地址会拒绝。
- Queue 名称或 VHost 为 URI 点段时工具 fail-closed，不发送 Purge。
- “重新检查并执行”会重新读取目标状态和生成计划。

- [ ] **Step 2: 执行静态安全审计**

```powershell
rg -n "DeleteAsync|HttpMethod\.Delete|\bDELETE\b" native/windows/src/NacosSyncTool.Windows
rg -n "Delete(VirtualHost|Queue|Exchange|Binding|Policy)|删除 (Virtual Host|Queue|Exchange|Binding|Policy)" native/windows/src/NacosSyncTool.Windows
rg -n "AllowAutoRedirect|PurgePathPattern|SendPurgeAsync" native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq
rg -n "Password|Authorization|UserInfo" native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq
git diff --name-only 295c894..HEAD | rg '^src/'
git diff --check
git status --short
```

Expected:

- 生产代码唯一 `HttpMethod.Delete` 位于 Queue `/contents` 实现。
- 不存在五类拓扑资源删除方法或 UI 文案。
- `AllowAutoRedirect = false`。
- Electron 根目录 `src/` 无改动。
- `git diff --check` 无错误。
- 工作区仅允许 `?? artifacts/`。

- [ ] **Step 3: 请求最终代码审查**

审查范围使用：

```powershell
git rev-parse 295c894
git rev-parse HEAD
```

审查必须逐项核对设计文档九条验收标准，并修复所有 Critical/Important 问题后重新审查。

- [ ] **Step 4: 推送并等待 GitHub Actions**

```powershell
git push origin codex/nacos-sync-tool-mvp
```

通过 GitHub API 检查本次 HEAD 对应 Run。Expected:

- Restore dependencies: success
- Build: success
- Run tests: success
- Publish Windows x64: success
- Upload Windows x64 artifact: success
- Publish Windows ARM64: success
- Upload Windows ARM64 artifact: success

- [ ] **Step 5: 失败时按系统化调试流程修复**

如果 GitHub Actions 失败：

1. 读取失败 step 和 annotations。
2. 确认首个编译或测试根因。
3. 添加或修正对应测试。
4. 使用最小补丁修复。
5. 提交、推送并重新等待完整工作流。

- [ ] **Step 6: 完成计划状态**

确认所有任务提交、远端 HEAD 一致、Actions 成功、工作区只有 `artifacts/` 后，将本计划所有 checkbox 更新为完成并提交：

```powershell
git add docs/superpowers/plans/2026-07-13-rabbitmq-hardening.md README.md native/windows/README.md
git commit -m "docs: complete RabbitMQ hardening rollout"
git push origin codex/nacos-sync-tool-mvp
```
