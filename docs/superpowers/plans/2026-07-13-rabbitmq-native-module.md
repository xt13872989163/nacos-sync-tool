# RabbitMQ Native Windows Module Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在现有原生 Windows WPF 应用中增加与 Nacos 隔离的 RabbitMQ 拓扑同步和 Virtual Host Queue Ready 消息清理功能。

**Architecture:** 保留现有 Nacos ViewModel、服务和同步行为，通过新的 Shell ViewModel 和独立 UserControl 把 Nacos 与 RabbitMQ 作为两个模块承载。RabbitMQ 使用 Management HTTP API，读取、计划生成、拓扑执行、消息清理和配置存储分别由独立服务负责；拓扑同步无 DELETE 能力，消息清理仅允许 Queue `/contents` Purge 路径。

**Tech Stack:** .NET 10、WPF、CommunityToolkit.Mvvm、HttpClient、System.Text.Json、Windows DPAPI、xUnit v3。

---

## 文件结构与职责

实施完成后的关键新增文件：

```text
native/windows/src/NacosSyncTool.Windows/
├─ ViewModels/
│  └─ ShellViewModel.cs                         # 应用模块切换
├─ Modules/
│  ├─ Nacos/Views/
│  │  ├─ NacosModuleView.xaml                  # 从 MainWindow 提取现有 Nacos UI
│  │  └─ NacosModuleView.xaml.cs
│  └─ RabbitMq/
│     ├─ Models/
│     │  ├─ RabbitMqConnection.cs              # 连接角色、连接信息和集群信息
│     │  ├─ RabbitMqResources.cs               # VHost/Exchange/Queue/Binding/Policy
│     │  ├─ RabbitMqSyncPlan.cs                # 计划、状态、快照和执行汇总
│     │  └─ RabbitMqQueuePurgeResult.cs        # 清理快照、单项和汇总
│     ├─ Services/
│     │  ├─ IRabbitMqManagementClient.cs       # 明确允许的 Management API 能力
│     │  ├─ RabbitMqManagementClient.cs        # HTTP、认证、序列化和路径安全
│     │  ├─ RabbitMqApiException.cs            # 不含敏感信息的 API 异常
│     │  ├─ RabbitMqClientFactory.cs           # 按连接角色创建独立客户端
│     │  ├─ RabbitMqResourceLoader.cs          # 读取、过滤、规范化资源
│     │  ├─ RabbitMqPlanBuilder.cs             # 名称比较和依赖分析
│     │  ├─ RabbitMqSyncExecutor.cs            # 顺序创建、重试、取消和汇总
│     │  ├─ RabbitMqQueuePurgeService.cs       # 唯一的 Queue /contents DELETE 调用方
│     │  ├─ RabbitMqSettingsStore.cs           # 独立配置与 DPAPI
│     │  └─ RabbitMqLogService.cs              # RabbitMQ 独立日志通道
│     ├─ ViewModels/
│     │  └─ RabbitMqModuleViewModel.cs         # 拓扑同步与消息清理页面状态
│     └─ Views/
│        ├─ RabbitMqModuleView.xaml
│        ├─ RabbitMqModuleView.xaml.cs
│        ├─ RabbitMqConfirmDialog.xaml
│        └─ RabbitMqConfirmDialog.xaml.cs
└─ MainWindow.xaml / MainWindow.xaml.cs          # 只保留应用外壳和主题
```

测试文件全部放在：

```text
native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/
```

---

### Task 1: 建立基线并提取应用外壳

**Files:**
- Create: `native/windows/src/NacosSyncTool.Windows/ViewModels/ShellViewModel.cs`
- Create: `native/windows/src/NacosSyncTool.Windows/Modules/Nacos/Views/NacosModuleView.xaml`
- Create: `native/windows/src/NacosSyncTool.Windows/Modules/Nacos/Views/NacosModuleView.xaml.cs`
- Create: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/ViewModels/RabbitMqModuleViewModel.cs`
- Create: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Views/RabbitMqModuleView.xaml`
- Create: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Views/RabbitMqModuleView.xaml.cs`
- Modify: `native/windows/src/NacosSyncTool.Windows/MainWindow.xaml`
- Modify: `native/windows/src/NacosSyncTool.Windows/MainWindow.xaml.cs`
- Modify: `native/windows/src/NacosSyncTool.Windows/App.xaml.cs`
- Test: `native/windows/tests/NacosSyncTool.Windows.Tests/ShellViewModelTests.cs`

- [ ] **Step 1: 运行原生版基线测试并记录通过数量**

Run:

```powershell
dotnet test native/windows/NacosSyncTool.Windows.sln --configuration Debug --verbosity minimal
```

Expected: exit code `0`，现有测试全部通过。

- [ ] **Step 2: 写 ShellViewModel 失败测试**

```csharp
using NacosSyncTool.Windows.Modules.RabbitMq.ViewModels;
using NacosSyncTool.Windows.ViewModels;
using Xunit;

namespace NacosSyncTool.Windows.Tests;

public sealed class ShellViewModelTests
{
    [Fact]
    public void DefaultsToNacosAndRetainsBothModuleInstances()
    {
        var nacos = new MainViewModel(new Services.LogService());
        var rabbit = (RabbitMqModuleViewModel)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(RabbitMqModuleViewModel));
        var shell = new ShellViewModel(nacos, rabbit);

        Assert.Equal(AppModule.Nacos, shell.SelectedModule);
        Assert.Same(nacos, shell.ActiveModule);

        shell.SelectedModule = AppModule.RabbitMq;
        Assert.Same(rabbit, shell.ActiveModule);

        shell.SelectedModule = AppModule.Nacos;
        Assert.Same(nacos, shell.ActiveModule);
    }
}
```

- [ ] **Step 3: 运行测试确认失败**

Run:

```powershell
dotnet test native/windows/NacosSyncTool.Windows.sln --filter FullyQualifiedName~ShellViewModelTests
```

Expected: FAIL，提示 `ShellViewModel`、`AppModule` 或 `RabbitMqModuleViewModel` 不存在。

- [ ] **Step 4: 实现最小 ShellViewModel 和 RabbitMQ 占位 ViewModel**

```csharp
// ViewModels/ShellViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using NacosSyncTool.Windows.Modules.RabbitMq.ViewModels;

namespace NacosSyncTool.Windows.ViewModels;

public enum AppModule { Nacos, RabbitMq }

public partial class ShellViewModel : ObservableObject
{
    public MainViewModel Nacos { get; }
    public RabbitMqModuleViewModel RabbitMq { get; }

    [ObservableProperty] private AppModule _selectedModule = AppModule.Nacos;
    [ObservableProperty] private object _activeModule;

    public ShellViewModel(MainViewModel nacos, RabbitMqModuleViewModel rabbitMq)
    {
        Nacos = nacos;
        RabbitMq = rabbitMq;
        _activeModule = nacos;
    }

    partial void OnSelectedModuleChanged(AppModule value) =>
        ActiveModule = value == AppModule.Nacos ? Nacos : RabbitMq;
}
```

```csharp
// Modules/RabbitMq/ViewModels/RabbitMqModuleViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace NacosSyncTool.Windows.Modules.RabbitMq.ViewModels;

public partial class RabbitMqModuleViewModel : ObservableObject { }
```

- [ ] **Step 5: 提取 Nacos UserControl，不改变绑定或事件行为**

把当前 `MainWindow.xaml` 中连接区、工作区、日志区和 Toast 原样移动到 `NacosModuleView.xaml`。把以下事件处理器从 `MainWindow.xaml.cs` 原样移动到 `NacosModuleView.xaml.cs`：

```text
OnSourcePasswordChanged
OnTargetPasswordChanged
OnScanKeyEnter
OnSelectableDataGridPreviewMouseLeftButtonDown
OnCopyValueClick
ShouldIgnoreRowToggle
FindVisualParent
GetVisualParent
```

`NacosModuleView` 不自行创建 ViewModel，由 Shell 绑定传入 `MainViewModel`。

- [ ] **Step 6: 将 MainWindow 改为模块外壳**

`MainWindow.xaml` 保留主题选择，增加两个模块按钮和一个 `ContentControl`：

```xml
<Window.Resources>
  <DataTemplate DataType="{x:Type viewModels:MainViewModel}">
    <nacosViews:NacosModuleView/>
  </DataTemplate>
  <DataTemplate DataType="{x:Type rabbitViewModels:RabbitMqModuleViewModel}">
    <rabbitViews:RabbitMqModuleView/>
  </DataTemplate>
</Window.Resources>

<StackPanel Orientation="Horizontal">
  <Button Content="Nacos" Command="{Binding SelectNacosCommand}"/>
  <Button Content="RabbitMQ" Command="{Binding SelectRabbitMqCommand}"/>
</StackPanel>
<ContentControl Content="{Binding ActiveModule}"/>
```

在 `ShellViewModel` 中加入命令：

```csharp
[CommunityToolkit.Mvvm.Input.RelayCommand]
private void SelectNacos() => SelectedModule = AppModule.Nacos;

[CommunityToolkit.Mvvm.Input.RelayCommand]
private void SelectRabbitMq() => SelectedModule = AppModule.RabbitMq;
```

- [ ] **Step 7: 注册依赖并回归测试**

`App.xaml.cs` 注册：

```csharp
services.AddSingleton<Modules.RabbitMq.ViewModels.RabbitMqModuleViewModel>();
services.AddSingleton<ViewModels.ShellViewModel>();
```

`MainWindow.xaml.cs` 的 `DataContext` 改为 `ShellViewModel`。

Run:

```powershell
dotnet test native/windows/NacosSyncTool.Windows.sln --configuration Debug
dotnet build native/windows/NacosSyncTool.Windows.sln --configuration Debug
```

Expected: 全部测试通过，构建 exit code `0`，启动后默认显示 Nacos，切换 RabbitMQ 再返回时 Nacos 状态对象未重建。

- [ ] **Step 8: Commit**

```powershell
git add native/windows/src/NacosSyncTool.Windows native/windows/tests/NacosSyncTool.Windows.Tests/ShellViewModelTests.cs
git commit -m "refactor: add native module shell"
```

---

### Task 2: 定义 RabbitMQ 资源、快照和计划模型

**Files:**
- Create: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Models/RabbitMqConnection.cs`
- Create: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Models/RabbitMqResources.cs`
- Create: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Models/RabbitMqSyncPlan.cs`
- Create: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Models/RabbitMqQueuePurgeResult.cs`
- Test: `native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqModelTests.cs`

- [ ] **Step 1: 写资源身份失败测试**

```csharp
using NacosSyncTool.Windows.Modules.RabbitMq.Models;
using Xunit;

namespace NacosSyncTool.Windows.Tests.RabbitMq;

public sealed class RabbitMqModelTests
{
    [Fact]
    public void BindingIdentityIgnoresArgumentsButIncludesRoutingKey()
    {
        var first = new RabbitMqBinding("events", "orders", RabbitMqBindingDestination.Queue, "created", new() { ["x-match"] = "all" });
        var second = new RabbitMqBinding("events", "orders", RabbitMqBindingDestination.Queue, "created", new());
        var different = second with { RoutingKey = "cancelled" };

        Assert.Equal(first.Identity, second.Identity);
        Assert.NotEqual(first.Identity, different.Identity);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run:

```powershell
dotnet test native/windows/NacosSyncTool.Windows.sln --filter FullyQualifiedName~RabbitMqModelTests
```

Expected: FAIL，RabbitMQ 模型尚不存在。

- [ ] **Step 3: 实现稳定的领域类型**

模型必须使用区分大小写的名称语义，并定义以下签名：

```csharp
public enum RabbitMqClientRole { SourceReadOnly, TargetTopology, QueuePurge }
public enum RabbitMqOperationMode { TopologySync, QueuePurge }
public enum RabbitMqSelectionMode { WholeVirtualHost, SelectedResources }
public enum RabbitMqBindingDestination { Queue, Exchange }
public enum RabbitMqResourceType { VirtualHost, Exchange, Policy, Queue, Binding }
public enum RabbitMqPlanStatus { PendingCreate, ExistingSkip, Unsupported, MissingDependency, Unknown }
public enum RabbitMqExecutionStatus { Created, Skipped, Failed, Cancelled, Missing }

public sealed record RabbitMqConnectionSettings(
    string Address,
    string Username,
    string Password,
    TimeSpan Timeout,
    bool ValidateServerCertificate = true);

public sealed record RabbitMqConnectionInfo(string RabbitMqVersion, string ClusterName);
public sealed record RabbitMqVirtualHost(string Name, string? Description, IReadOnlyList<string> Tags, string? DefaultQueueType);
public sealed record RabbitMqExchange(string Name, string Type, bool Durable, bool AutoDelete, bool Internal, Dictionary<string, object?> Arguments);
public sealed record RabbitMqQueue(string Name, bool Durable, bool AutoDelete, bool Exclusive, Dictionary<string, object?> Arguments, long MessagesReady, long MessagesUnacknowledged, int Consumers);
public sealed record RabbitMqPolicy(string Name, string Pattern, Dictionary<string, object?> Definition, int Priority, string ApplyTo);
public sealed record RabbitMqBinding(string Source, string Destination, RabbitMqBindingDestination DestinationType, string RoutingKey, Dictionary<string, object?> Arguments)
{
    public string Identity => $"{Source}\u001f{Destination}\u001f{DestinationType}\u001f{RoutingKey}";
}

public sealed record RabbitMqTopologySnapshot(
    RabbitMqVirtualHost VirtualHost,
    IReadOnlyList<RabbitMqExchange> Exchanges,
    IReadOnlyList<RabbitMqPolicy> Policies,
    IReadOnlyList<RabbitMqQueue> Queues,
    IReadOnlyList<RabbitMqBinding> Bindings);

public partial class RabbitMqSelectionItem<T> : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public T Resource { get; }
    public string Identity { get; }
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private bool _isSelected;

    public RabbitMqSelectionItem(T resource, string identity, bool isSelected = true)
    {
        Resource = resource;
        Identity = identity;
        _isSelected = isSelected;
    }
}
```

同步计划和清理结果必须定义：

```csharp
public sealed record RabbitMqSyncPlanItem(RabbitMqResourceType Type, string Identity, object Resource, RabbitMqPlanStatus Status, string Reason);
public sealed record RabbitMqSyncPlan(string VirtualHost, DateTimeOffset CreatedAt, IReadOnlyList<RabbitMqSyncPlanItem> Items);
public sealed record RabbitMqExecutionItem(RabbitMqResourceType Type, string Identity, RabbitMqExecutionStatus Status, string Message);
public sealed record RabbitMqExecutionSummary(IReadOnlyList<RabbitMqExecutionItem> Items);

public sealed record RabbitMqQueuePurgePreview(
    string VirtualHost,
    IReadOnlyList<RabbitMqQueue> Queues,
    long ReadyTotal,
    long UnackedTotal);
public sealed record RabbitMqQueuePurgeItem(string QueueName, long ReadyBefore, RabbitMqExecutionStatus Status, string Message);
public sealed record RabbitMqQueuePurgeSummary(string VirtualHost, IReadOnlyList<RabbitMqQueuePurgeItem> Items, long ReadyAfter, long UnackedAfter, bool Cancelled);
```

- [ ] **Step 4: 运行测试并提交**

Run:

```powershell
dotnet test native/windows/NacosSyncTool.Windows.sln --filter FullyQualifiedName~RabbitMqModelTests
```

Expected: PASS。

```powershell
git add native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Models native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqModelTests.cs
git commit -m "feat: add RabbitMQ domain models"
```

---

### Task 3: 实现 Management API 地址、认证与角色安全层

**Files:**
- Create: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/IRabbitMqManagementClient.cs`
- Create: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/RabbitMqManagementClient.cs`
- Create: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/RabbitMqApiException.cs`
- Create: `native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RecordingHttpMessageHandler.cs`
- Create: `native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/FakeRabbitMqManagementClient.cs`
- Test: `native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqManagementClientSecurityTests.cs`

- [ ] **Step 1: 写 URL 编码和禁止删除测试**

```csharp
[Fact]
public async Task EncodesDefaultVhostAndRejectsNonPurgeDelete()
{
    var handler = new RecordingHttpMessageHandler(HttpStatusCode.NoContent, "");
    var client = new RabbitMqManagementClient(Settings(), RabbitMqClientRole.QueuePurge, handler);

    await client.PurgeQueueAsync("/", "orders/a", CancellationToken.None);

    Assert.Equal(HttpMethod.Delete, handler.LastRequest!.Method);
    Assert.EndsWith("api/queues/%2F/orders%2Fa/contents", handler.LastRequest.RequestUri!.AbsoluteUri);
    Assert.DoesNotContain(client.GetType().GetMethods(), method =>
        method.Name is "DeleteQueueAsync" or "DeleteVirtualHostAsync" or "DeleteExchangeAsync");
}

[Fact]
public async Task PreservesReverseProxyBasePath()
{
    var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, "{\"rabbitmq_version\":\"4.1\",\"cluster_name\":\"cluster-a\"}");
    var client = new RabbitMqManagementClient(
        Settings(address: "https://gateway.example/rabbit/"),
        RabbitMqClientRole.SourceReadOnly,
        handler);

    await client.TestConnectionAsync(CancellationToken.None);

    Assert.Equal("/rabbit/api/overview", handler.LastRequest!.RequestUri!.AbsolutePath);
}
```

- [ ] **Step 2: 运行测试确认失败**

Run:

```powershell
dotnet test native/windows/NacosSyncTool.Windows.sln --filter FullyQualifiedName~RabbitMqManagementClientSecurityTests
```

Expected: FAIL，客户端和测试 Handler 不存在。

- [ ] **Step 3: 定义显式接口，不暴露通用删除能力**

```csharp
public interface IRabbitMqManagementClient
{
    Task<RabbitMqConnectionInfo> TestConnectionAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<RabbitMqVirtualHost>> GetVirtualHostsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<RabbitMqExchange>> GetExchangesAsync(string virtualHost, CancellationToken cancellationToken);
    Task<IReadOnlyList<RabbitMqQueue>> GetQueuesAsync(string virtualHost, CancellationToken cancellationToken);
    Task<IReadOnlyList<RabbitMqBinding>> GetBindingsAsync(string virtualHost, CancellationToken cancellationToken);
    Task<IReadOnlyList<RabbitMqPolicy>> GetPoliciesAsync(string virtualHost, CancellationToken cancellationToken);
    Task CreateVirtualHostAsync(RabbitMqVirtualHost virtualHost, CancellationToken cancellationToken);
    Task CreateExchangeAsync(string virtualHost, RabbitMqExchange exchange, CancellationToken cancellationToken);
    Task CreateQueueAsync(string virtualHost, RabbitMqQueue queue, CancellationToken cancellationToken);
    Task CreateBindingAsync(string virtualHost, RabbitMqBinding binding, CancellationToken cancellationToken);
    Task CreatePolicyAsync(string virtualHost, RabbitMqPolicy policy, CancellationToken cancellationToken);
    Task PurgeQueueAsync(string virtualHost, string queueName, CancellationToken cancellationToken);
}
```

接口中不得出现 DeleteVirtualHost、DeleteQueue、DeleteExchange、DeleteBinding、DeletePolicy 或通用 `DeleteAsync`。

- [ ] **Step 4: 实现基础 HTTP 管道和路径白名单**

实现要求：

```csharp
private static string Segment(string value) => Uri.EscapeDataString(value);

private async Task<HttpResponseMessage> SendDeleteAsync(string relativePath, CancellationToken cancellationToken)
{
    if (_role != RabbitMqClientRole.QueuePurge ||
        !Regex.IsMatch(relativePath, @"^api/queues/[^/]+/[^/]+/contents$", RegexOptions.CultureInvariant))
    {
        throw new InvalidOperationException("Only Queue /contents purge is allowed.");
    }

    return await _httpClient.DeleteAsync(relativePath, cancellationToken);
}
```

构造函数必须：规范化尾部 `/`、配置 Basic Authentication、设置超时、按设置处理 HTTPS 证书验证、使用注入的 `HttpMessageHandler` 支持单元测试。

`RecordingHttpMessageHandler` 保存收到的请求方法、URI 和 Body，并按队列返回预设响应。`FakeRabbitMqManagementClient` 完整实现 `IRabbitMqManagementClient`：GET 方法返回可配置集合，Create/Purge 方法记录调用，测试可为指定调用配置异常。后续 Loader、Plan、Executor 和 ViewModel 测试统一复用这两个测试替身，不在各测试文件重复实现。

- [ ] **Step 5: 运行安全测试并提交**

Run:

```powershell
dotnet test native/windows/NacosSyncTool.Windows.sln --filter FullyQualifiedName~RabbitMqManagementClientSecurityTests
```

Expected: PASS，记录到的唯一 DELETE 路径以 `/contents` 结尾。

```powershell
git add native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq
git commit -m "feat: add secure RabbitMQ management transport"
```

---

### Task 4: 实现 Management API 读取能力

**Files:**
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/RabbitMqManagementClient.cs`
- Test: `native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqManagementClientReadTests.cs`

- [ ] **Step 1: 写 Overview、VHost 和资源反序列化测试**

使用 `RecordingHttpMessageHandler` 为以下路径返回固定 JSON：

```text
api/overview
api/vhosts
api/exchanges/%2F
api/queues/%2F
api/bindings/%2F
api/policies/%2F
```

断言：版本、集群名、`messages_ready`、`messages_unacknowledged`、`exclusive`、Binding destination type、Policy definition 均正确映射。

- [ ] **Step 2: 运行测试确认失败**

Run:

```powershell
dotnet test native/windows/NacosSyncTool.Windows.sln --filter FullyQualifiedName~RabbitMqManagementClientReadTests
```

Expected: FAIL，读取方法尚未完成。

- [ ] **Step 3: 实现只读端点**

使用 `System.Text.Json`，配置：

```csharp
private static readonly JsonSerializerOptions JsonOptions = new()
{
    PropertyNameCaseInsensitive = true
};
```

DTO 使用 `[JsonPropertyName]` 映射 snake_case。未知字段自动忽略。每个非成功响应统一抛出包含状态码、路径和 RabbitMQ 响应正文摘要的 `RabbitMqApiException`，但异常消息不得包含 Authorization Header 或密码。

异常类型定义为：

```csharp
public sealed class RabbitMqApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string RequestPath { get; }

    public RabbitMqApiException(HttpStatusCode statusCode, string requestPath, string responseSummary)
        : base($"RabbitMQ API {statusCode} at {requestPath}: {responseSummary}")
    {
        StatusCode = statusCode;
        RequestPath = requestPath;
    }
}
```

- [ ] **Step 4: 验证读取和源端角色限制**

增加测试：`SourceReadOnly` 可以调用所有 GET；调用任何 Create 或 Purge 方法抛出 `InvalidOperationException`，且 Handler 没有收到请求。

Run:

```powershell
dotnet test native/windows/NacosSyncTool.Windows.sln --filter FullyQualifiedName~RabbitMqManagementClientReadTests
```

Expected: PASS。

- [ ] **Step 5: Commit**

```powershell
git add native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/RabbitMqManagementClient.cs native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqManagementClientReadTests.cs
git commit -m "feat: read RabbitMQ management resources"
```

---

### Task 5: 实现显式创建和 Queue Purge 请求

**Files:**
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/RabbitMqManagementClient.cs`
- Test: `native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqManagementClientWriteTests.cs`

- [ ] **Step 1: 写请求方法、路径和 JSON Body 测试**

覆盖：

```text
PUT  api/vhosts/{name}
PUT  api/exchanges/{vhost}/{name}
PUT  api/policies/{vhost}/{name}
PUT  api/queues/{vhost}/{name}
POST api/bindings/{vhost}/e/{source}/q/{destination}
POST api/bindings/{vhost}/e/{source}/e/{destination}
DELETE api/queues/{vhost}/{queue}/contents
```

断言 Exchange body 包含 `type/durable/auto_delete/internal/arguments`，Queue body 不包含 `exclusive`，Policy body 包含 `pattern/definition/priority/apply-to`，Binding body 包含 `routing_key/arguments`。

增加 Quorum/Stream Queue 用例，断言 `arguments` 中的 `x-queue-type` 和其他未知参数原样发送；目标返回不支持错误时客户端保留原始失败信息，不删除参数后重试。

- [ ] **Step 2: 运行测试确认失败**

Run:

```powershell
dotnet test native/windows/NacosSyncTool.Windows.sln --filter FullyQualifiedName~RabbitMqManagementClientWriteTests
```

Expected: FAIL，写入实现尚未完成。

- [ ] **Step 3: 实现角色校验和显式写入方法**

规则：

```text
SourceReadOnly: 仅 GET
TargetTopology: GET + 五类拓扑创建，禁止 Purge
QueuePurge: GET + Queue /contents Purge，禁止拓扑创建
```

`PurgeQueueAsync` 必须只调用 Task 3 的白名单 `SendDeleteAsync`。不得添加任何资源删除方法。

- [ ] **Step 4: 运行写入与安全测试**

Run:

```powershell
dotnet test native/windows/NacosSyncTool.Windows.sln --filter "FullyQualifiedName~RabbitMqManagementClientWriteTests|FullyQualifiedName~RabbitMqManagementClientSecurityTests"
```

Expected: PASS。

- [ ] **Step 5: Commit**

```powershell
git add native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/RabbitMqManagementClient.cs native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqManagementClientWriteTests.cs
git commit -m "feat: create RabbitMQ topology and purge queue contents"
```

---

### Task 6: 加载并过滤可同步资源

**Files:**
- Create: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/RabbitMqResourceLoader.cs`
- Test: `native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqResourceLoaderTests.cs`

- [ ] **Step 1: 写过滤失败测试**

```csharp
[Fact]
public async Task FiltersBuiltInExchangesGeneratedQueuesAndExclusiveQueuesForSync()
{
    var client = new FakeRabbitMqClient
    {
        Exchanges = [Exchange(""), Exchange("amq.direct"), Exchange("orders")],
        Queues = [Queue("amq.gen-abc"), Queue("exclusive", exclusive: true), Queue("orders")]
    };

    var snapshot = await new RabbitMqResourceLoader(client).LoadSyncSnapshotAsync("/", CancellationToken.None);

    Assert.Equal(["orders"], snapshot.Exchanges.Select(x => x.Name));
    Assert.Equal(["orders"], snapshot.Queues.Select(x => x.Name));
}
```

- [ ] **Step 2: 运行测试确认失败**

Run:

```powershell
dotnet test native/windows/NacosSyncTool.Windows.sln --filter FullyQualifiedName~RabbitMqResourceLoaderTests
```

Expected: FAIL。

- [ ] **Step 3: 实现两个加载入口**

```csharp
Task<RabbitMqTopologySnapshot> LoadSyncSnapshotAsync(string virtualHost, CancellationToken cancellationToken);
Task<IReadOnlyList<RabbitMqQueue>> LoadPurgeQueuesAsync(string virtualHost, CancellationToken cancellationToken);
```

`LoadSyncSnapshotAsync` 过滤默认 Exchange、`amq.*`、`amq.gen-*` 和 Exclusive Queue；`LoadPurgeQueuesAsync` 不过滤任何可访问 Queue，因为清理范围是当前 Virtual Host 的全部 Queue。

- [ ] **Step 4: 运行测试并提交**

```powershell
dotnet test native/windows/NacosSyncTool.Windows.sln --filter FullyQualifiedName~RabbitMqResourceLoaderTests
git add native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/RabbitMqResourceLoader.cs native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq
git commit -m "feat: load and filter RabbitMQ resources"
```

---

### Task 7: 生成幂等同步计划

**Files:**
- Create: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/RabbitMqPlanBuilder.cs`
- Test: `native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqPlanBuilderTests.cs`

- [ ] **Step 1: 写按名称跳过和 Binding 依赖测试**

测试必须覆盖：

- 同名 Exchange、Queue、Policy 配置不同仍标记 `ExistingSkip`。
- 目标没有同名资源时标记 `PendingCreate`。
- Binding 按 Source、Destination、DestinationType、RoutingKey 判断，忽略 arguments。
- 选择 Binding 但依赖资源既不在目标也不在本次选择时标记 `MissingDependency`。
- 计划顺序固定为 VirtualHost、Exchange、Policy、Queue、Binding。

- [ ] **Step 2: 运行测试确认失败**

```powershell
dotnet test native/windows/NacosSyncTool.Windows.sln --filter FullyQualifiedName~RabbitMqPlanBuilderTests
```

Expected: FAIL。

- [ ] **Step 3: 实现纯函数计划构建器**

公开签名：

```csharp
public RabbitMqSyncPlan Build(
    RabbitMqTopologySnapshot source,
    RabbitMqTopologySnapshot? target,
    IReadOnlySet<string> selectedExchangeNames,
    IReadOnlySet<string> selectedPolicyNames,
    IReadOnlySet<string> selectedQueueNames,
    IReadOnlySet<string> selectedBindingIdentities);
```

比较器全部使用 `StringComparer.Ordinal`。当 `target` 为 `null` 时，计划先创建同名 Virtual Host，再计划选中资源。

- [ ] **Step 4: 运行测试并提交**

```powershell
dotnet test native/windows/NacosSyncTool.Windows.sln --filter FullyQualifiedName~RabbitMqPlanBuilderTests
git add native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/RabbitMqPlanBuilder.cs native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqPlanBuilderTests.cs
git commit -m "feat: build RabbitMQ sync plans"
```

---

### Task 8: 执行同步计划、重试并支持取消

**Files:**
- Create: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/RabbitMqSyncExecutor.cs`
- Test: `native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqSyncExecutorTests.cs`

- [ ] **Step 1: 写顺序、失败继续和取消测试**

使用 Fake Client 记录调用，断言：

```text
CreateVirtualHost
CreateExchange
CreatePolicy
CreateQueue
CreateBinding
```

网络超时或 `5xx` 最多执行三次总尝试；`401/403` 不重试；单个 Exchange 失败后无依赖 Policy/Queue 继续；依赖 Exchange 失败的 Binding 不调用 API；取消后不处理后续项。

- [ ] **Step 2: 运行测试确认失败**

```powershell
dotnet test native/windows/NacosSyncTool.Windows.sln --filter FullyQualifiedName~RabbitMqSyncExecutorTests
```

Expected: FAIL。

- [ ] **Step 3: 实现执行器**

公开签名：

```csharp
public async Task<RabbitMqExecutionSummary> ExecuteAsync(
    RabbitMqSyncPlan plan,
    IProgress<(int Completed, int Total, string Current)>? progress,
    CancellationToken cancellationToken);
```

执行前对每项 `PendingCreate` 再读取目标快照或调用显式存在性查询。若资源已经出现，记录 `Skipped`。重试仅处理 `HttpRequestException`、`TaskCanceledException`（非用户取消）和 `RabbitMqApiException` 的 `5xx`。

- [ ] **Step 4: 确认执行器没有 DELETE 能力**

测试使用反射断言 `RabbitMqSyncExecutor` 没有名称包含 `Delete` 或 `Purge` 的公开方法，并确认 Fake Client 的 `PurgeQueueAsync` 调用数为 0。

- [ ] **Step 5: 运行测试并提交**

```powershell
dotnet test native/windows/NacosSyncTool.Windows.sln --filter FullyQualifiedName~RabbitMqSyncExecutorTests
git add native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/RabbitMqSyncExecutor.cs native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqSyncExecutorTests.cs
git commit -m "feat: execute RabbitMQ sync plans"
```

---

### Task 9: 独立保存 RabbitMQ 三种连接角色

**Files:**
- Create: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/RabbitMqSettingsStore.cs`
- Test: `native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqSettingsStoreTests.cs`

- [ ] **Step 1: 写隔离与加密失败测试**

测试使用临时目录，保存 Source、Target、QueuePurge 三组连接后断言：

- 三组地址和用户名分别恢复。
- 文件中不包含任何明文密码。
- 清除 QueuePurge 不影响 Source 和 Target。
- 存储文件位于注入路径，不读取 Nacos 现有设置。

- [ ] **Step 2: 运行测试确认失败**

```powershell
dotnet test native/windows/NacosSyncTool.Windows.sln --filter FullyQualifiedName~RabbitMqSettingsStoreTests
```

Expected: FAIL。

- [ ] **Step 3: 实现 DPAPI 设置存储**

默认路径：

```csharp
Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "NacosSyncTool",
    "rabbitmq-settings.json")
```

密码使用：

```csharp
ProtectedData.Protect(Encoding.UTF8.GetBytes(password), null, DataProtectionScope.CurrentUser)
ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser)
```

公开方法：

```csharp
public sealed record RabbitMqSavedConnection(
    string Address,
    string Username,
    string Password,
    int TimeoutSeconds,
    bool ValidateServerCertificate,
    string? SelectedVirtualHost);

Task<RabbitMqSavedConnection?> LoadAsync(RabbitMqClientRole role, CancellationToken cancellationToken);
Task SaveAsync(RabbitMqClientRole role, RabbitMqSavedConnection connection, CancellationToken cancellationToken);
Task ClearAsync(RabbitMqClientRole role, CancellationToken cancellationToken);
```

JSON 文档内部使用独立 DTO 保存 `EncryptedPassword` 的 Base64 字符串；公共 `RabbitMqSavedConnection` 只在内存中短暂持有明文密码，不允许写日志。

- [ ] **Step 4: 运行测试并提交**

```powershell
dotnet test native/windows/NacosSyncTool.Windows.sln --filter FullyQualifiedName~RabbitMqSettingsStoreTests
git add native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/RabbitMqSettingsStore.cs native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqSettingsStoreTests.cs
git commit -m "feat: persist encrypted RabbitMQ settings"
```

---

### Task 10: 实现 Virtual Host Queue Ready 消息清理服务

**Files:**
- Create: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/RabbitMqQueuePurgeService.cs`
- Test: `native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqQueuePurgeServiceTests.cs`

- [ ] **Step 1: 写 Ready 统计、顺序清理和 Unacked 保留测试**

测试 Queue：

```text
orders: ready=10, unacked=3
payments: ready=5, unacked=2
```

断言确认摘要 Ready 为 15、Unacked 为 5；服务只对两个 Queue 调用 `PurgeQueueAsync`；最终结果不声称清除了 Unacked。

- [ ] **Step 2: 写错误和取消测试**

覆盖：

- `404` 记录 Missing 并继续。
- `401/403` 立即停止。
- 临时 `5xx` 最多重试两次。
- 用户取消后不再调用后续 Queue。
- 清理结束后重新读取 Queue，并汇总 `ReadyAfter`、`UnackedAfter`。

- [ ] **Step 3: 运行测试确认失败**

```powershell
dotnet test native/windows/NacosSyncTool.Windows.sln --filter FullyQualifiedName~RabbitMqQueuePurgeServiceTests
```

Expected: FAIL。

- [ ] **Step 4: 实现清理服务**

公开签名：

```csharp
public async Task<RabbitMqQueuePurgePreview> BuildPreviewAsync(string virtualHost, CancellationToken cancellationToken);

public async Task<RabbitMqQueuePurgeSummary> ExecuteAsync(
    RabbitMqQueuePurgePreview preview,
    IProgress<(int Completed, int Total, string Current)>? progress,
    CancellationToken cancellationToken);
```

`RabbitMqQueuePurgePreview` 固定 Virtual Host、Queue 快照、Ready 总数和 Unacked 总数。执行器不得重新扩大快照范围；新建 Queue 留到下一次操作。

- [ ] **Step 5: 验证唯一 DELETE 能力并提交**

Run:

```powershell
dotnet test native/windows/NacosSyncTool.Windows.sln --filter "FullyQualifiedName~RabbitMqQueuePurgeServiceTests|FullyQualifiedName~RabbitMqManagementClientSecurityTests"
```

Expected: PASS；所有记录的 DELETE 均以 `/contents` 结尾。

```powershell
git add native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/RabbitMqQueuePurgeService.cs native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq
git commit -m "feat: purge RabbitMQ virtual host queue messages"
```

---

### Task 11: 增加 RabbitMQ 独立日志与确认模型

**Files:**
- Create: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/RabbitMqLogService.cs`
- Create: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Models/RabbitMqConfirmation.cs`
- Create: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/IRabbitMqConfirmationService.cs`
- Create: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Services/RabbitMqConfirmationService.cs`
- Create: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Views/RabbitMqConfirmDialog.xaml`
- Create: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Views/RabbitMqConfirmDialog.xaml.cs`
- Test: `native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqConfirmationTests.cs`

- [ ] **Step 1: 写确认内容失败测试**

```csharp
[Fact]
public void PurgeConfirmationContainsCountsWithoutTypedNameRequirement()
{
    var request = RabbitMqConfirmation.BuildPurge("cluster-a", "http://mq:15672", "/dev", 4, 120, 8);

    Assert.Contains("/dev", request.Body);
    Assert.Contains("120", request.Body);
    Assert.Contains("8", request.Body);
    Assert.False(request.RequiresTypedConfirmation);
    Assert.Equal("确认清空", request.ConfirmText);
}
```

- [ ] **Step 2: 实现确认请求与对话框**

```csharp
public sealed record RabbitMqConfirmationRequest(
    string Title,
    string Body,
    string ConfirmText,
    bool IsDangerous,
    bool RequiresTypedConfirmation = false);

public static class RabbitMqConfirmation
{
    public static RabbitMqConfirmationRequest BuildPurge(
        string cluster,
        string address,
        string virtualHost,
        int queueCount,
        long ready,
        long unacked) => new(
            "确认清空 Queue Ready 消息",
            $"集群：{cluster}\n地址：{address}\nVirtual Host：{virtualHost}\nQueue：{queueCount}\nReady：{ready}\nUnacked：{unacked}（不会清除）\n操作不可恢复，生产者仍可能继续写入。",
            "确认清空",
            true);
}
```

对话框只有确认和取消按钮。危险操作确认按钮使用危险色，取消按钮为默认按钮并在打开时获得焦点；不得加入 Virtual Host 文本输入框。

ViewModel 通过可替换接口请求确认：

```csharp
public interface IRabbitMqConfirmationService
{
    Task<bool> ConfirmAsync(RabbitMqConfirmationRequest request);
}
```

`RabbitMqConfirmationService` 在 WPF Dispatcher 上打开 `RabbitMqConfirmDialog`；单元测试注入返回固定 true/false 的 Fake，不启动窗口。

- [ ] **Step 3: 实现独立日志通道**

RabbitMQ 日志写入：

```text
%LOCALAPPDATA%/NacosSyncTool/logs/rabbitmq.log
```

日志 API 与现有 `LogService` 保持 Info/Success/Warning/Error 风格，但事件集合完全独立。认证信息写日志前必须经过敏感字段过滤。

- [ ] **Step 4: 测试并提交**

```powershell
dotnet test native/windows/NacosSyncTool.Windows.sln --filter FullyQualifiedName~RabbitMqConfirmationTests
git add native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqConfirmationTests.cs
git commit -m "feat: add RabbitMQ confirmations and logs"
```

---

### Task 12: 实现 RabbitMQ 拓扑同步 ViewModel 流程

**Files:**
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/ViewModels/RabbitMqModuleViewModel.cs`
- Test: `native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqTopologyViewModelTests.cs`

- [ ] **Step 1: 写连接、计划失效和执行前确认测试**

使用接口 Fake 注入，覆盖：

- 默认模式为 `TopologySync`。
- 源端和目标端连接状态互相独立。
- 选择源 Virtual Host 后目标名称自动同名。
- 切换 Virtual Host、刷新资源或修改选择后 `CurrentPlan` 变为 null。
- 没有计划时执行命令不可用。
- 用户取消确认时执行器调用数为 0。
- 整套同步选择所有可同步项；选择性同步只使用当前勾选项。
- 执行完成后能够基于失败项生成新的重试计划。
- ViewModel 初始化时分别加载 Source 和 Target 设置；连接成功后保存对应角色设置和最近 Virtual Host。

- [ ] **Step 2: 运行测试确认失败**

```powershell
dotnet test native/windows/NacosSyncTool.Windows.sln --filter FullyQualifiedName~RabbitMqTopologyViewModelTests
```

Expected: FAIL。

- [ ] **Step 3: 实现拓扑属性和命令**

至少包含：

```text
SourceAddress/Username/Password/IsConnected/VirtualHosts
TargetAddress/Username/Password/IsConnected
SelectedSourceVirtualHost/TargetVirtualHostStatus
Exchanges/Queues/Bindings/Policies
SelectionMode/SearchText
SelectedResourceTab
CurrentPlan/PlanSummary
IsBusy/BusyText/Progress
TestSourceConnectionCommand
TestTargetConnectionCommand
RefreshResourcesCommand
BuildPlanCommand
ExecutePlanCommand
RetryFailedCommand
CancelCommand
```

ViewModel 只调用 Loader、PlanBuilder、Executor、SettingsStore 和确认服务，不直接构造 URL 或 HTTP 请求。

资源集合使用 `RabbitMqSelectionItem<T>`，搜索和排序通过 `ICollectionView` 实现；搜索仅影响显示，不改变已经勾选的选择状态。

用以下构造函数替换 Task 1 的占位实现：

```csharp
public RabbitMqModuleViewModel(
    IRabbitMqClientFactory clientFactory,
    RabbitMqPlanBuilder planBuilder,
    RabbitMqSettingsStore settingsStore,
    RabbitMqLogService logService,
    IRabbitMqConfirmationService confirmationService)
```

- [ ] **Step 4: 运行测试并提交**

```powershell
dotnet test native/windows/NacosSyncTool.Windows.sln --filter FullyQualifiedName~RabbitMqTopologyViewModelTests
git add native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/ViewModels/RabbitMqModuleViewModel.cs native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqTopologyViewModelTests.cs
git commit -m "feat: add RabbitMQ topology workflow"
```

---

### Task 13: 实现消息清理 ViewModel 流程

**Files:**
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/ViewModels/RabbitMqModuleViewModel.cs`
- Test: `native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqPurgeViewModelTests.cs`

- [ ] **Step 1: 写独立连接和单次确认测试**

覆盖：

- 切换到 `QueuePurge` 不复用 Source/Target 连接状态。
- Purge 连接成功后加载 Virtual Host。
- 选择 Virtual Host 后加载全部 Queue，包括 `amq.gen-*` 和 Exclusive Queue。
- 点击清空只生成一次确认请求。
- 确认正文包含 Ready 和 Unacked；不要求输入 Virtual Host。
- 确认取消时没有 Purge 调用。
- 执行期间允许 Cancel，结束后刷新列表。
- ViewModel 初始化时加载 QueuePurge 设置；连接成功和 Virtual Host 变化后保存 QueuePurge 角色设置。

- [ ] **Step 2: 运行测试确认失败**

```powershell
dotnet test native/windows/NacosSyncTool.Windows.sln --filter FullyQualifiedName~RabbitMqPurgeViewModelTests
```

Expected: FAIL。

- [ ] **Step 3: 实现清理属性和命令**

至少包含：

```text
PurgeAddress/Username/Password/IsConnected
PurgeVirtualHosts/SelectedPurgeVirtualHost
PurgeQueues
ReadyTotal/UnackedTotal/ConsumerTotal
PurgeProgress/PurgeSummary
TestPurgeConnectionCommand
RefreshPurgeQueuesCommand
PurgeAllReadyMessagesCommand
CancelCommand
```

`PurgeAllReadyMessagesCommand` 的固定顺序：BuildPreview → 单次确认 → Execute → Refresh。

- [ ] **Step 4: 运行测试并提交**

```powershell
dotnet test native/windows/NacosSyncTool.Windows.sln --filter FullyQualifiedName~RabbitMqPurgeViewModelTests
git add native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/ViewModels/RabbitMqModuleViewModel.cs native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqPurgeViewModelTests.cs
git commit -m "feat: add RabbitMQ queue purge workflow"
```

---

### Task 14: 完成 RabbitMQ WPF 界面和密码事件绑定

**Files:**
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Views/RabbitMqModuleView.xaml`
- Modify: `native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Views/RabbitMqModuleView.xaml.cs`
- Modify: `native/windows/src/NacosSyncTool.Windows/Themes/Shared.xaml`
- Test: `native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqViewBindingTests.cs`

- [ ] **Step 1: 写 XAML 绑定契约测试**

读取 XAML 文本并断言包含：

```text
TestSourceConnectionCommand
TestTargetConnectionCommand
BuildPlanCommand
ExecutePlanCommand
TestPurgeConnectionCommand
RefreshPurgeQueuesCommand
PurgeAllReadyMessagesCommand
Exchange / Queue / Binding / Policy 页签
Ready / Unacked / 消费者 列
```

同时断言不存在以下文本：

```text
删除 Virtual Host
删除 Queue
删除 Exchange
DeleteQueueCommand
DeleteVirtualHostCommand
```

- [ ] **Step 2: 实现拓扑同步页面**

页面结构：模块内“拓扑同步 / 队列消息清理”固定切换、源/目标连接卡、同名 Virtual Host 状态、资源页签、选择工具栏、计划摘要、执行/取消按钮和 RabbitMQ 独立日志。

计划未生成时 `ExecutePlanCommand` 对应按钮禁用；选择变化通过 ViewModel 使计划失效。

资源状态同时显示图标和文字，不能只使用颜色。资源名称支持复制；Exchange/Queue arguments 和 Policy definition 使用摘要显示，并提供格式化 JSON 详情弹层。

- [ ] **Step 3: 实现消息清理页面**

页面结构：独立操作端连接、Virtual Host 下拉框、只读 Queue DataGrid、Ready/Unacked/Consumers/Total 列、刷新按钮、危险样式“一键清空 Ready 消息”按钮、进度和汇总。

页面中不得出现任何资源删除按钮或上下文菜单。

- [ ] **Step 4: 绑定 PasswordBox**

在 `RabbitMqModuleView.xaml.cs` 分别处理 Source、Target、Purge PasswordBox 的 `PasswordChanged`，只写入对应 ViewModel 属性，不在 XAML、日志或异常中回显密码。

- [ ] **Step 5: 解析 XAML、运行测试并提交**

Run:

```powershell
[xml](Get-Content -Raw native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Views/RabbitMqModuleView.xaml) | Out-Null
[xml](Get-Content -Raw native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Views/RabbitMqConfirmDialog.xaml) | Out-Null
dotnet test native/windows/NacosSyncTool.Windows.sln --filter FullyQualifiedName~RabbitMqViewBindingTests
dotnet build native/windows/NacosSyncTool.Windows.sln --configuration Debug
```

Expected: XML 解析无异常，测试和构建通过。

```powershell
git add native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq/Views native/windows/src/NacosSyncTool.Windows/Themes/Shared.xaml native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqViewBindingTests.cs
git commit -m "feat: add RabbitMQ native Windows interface"
```

---

### Task 15: 完成依赖注入、工厂和产品显示信息

**Files:**
- Modify: `native/windows/src/NacosSyncTool.Windows/App.xaml.cs`
- Modify: `native/windows/src/NacosSyncTool.Windows/Models/AppMetadata.cs`
- Modify: `native/windows/src/NacosSyncTool.Windows/MainWindow.xaml`
- Test: `native/windows/tests/NacosSyncTool.Windows.Tests/AppMetadataTests.cs`
- Test: `native/windows/tests/NacosSyncTool.Windows.Tests/RabbitMq/RabbitMqDependencyGraphTests.cs`

- [ ] **Step 1: 增加客户端工厂**

注册一个显式工厂：

```csharp
public interface IRabbitMqClientFactory
{
    IRabbitMqManagementClient Create(RabbitMqConnectionSettings settings, RabbitMqClientRole role);
}
```

实现文件为 `Modules/RabbitMq/Services/RabbitMqClientFactory.cs`，只负责根据 Settings 和 Role 创建新 `RabbitMqManagementClient`，不得缓存或跨角色复用实例。

ViewModel 每次测试新连接时创建对应角色客户端，Source、Target、QueuePurge 不共享实例。

- [ ] **Step 2: 注册全部 RabbitMQ 服务**

`App.xaml.cs` 注册 Factory、SettingsStore、RabbitMqLogService、PlanBuilder、RabbitMqModuleViewModel 和 ShellViewModel。ResourceLoader、Executor、PurgeService 使用当前连接客户端构造，不注册为错误共享角色的 Singleton。

- [ ] **Step 3: 调整显示名称但保持可执行文件兼容**

将显示标题改为 `Nacos / RabbitMQ Sync Tool`，但保持 `.csproj` 的 `AssemblyName` 和便携包文件名不变，避免破坏现有分发路径。

更新测试：

```csharp
Assert.Equal("Nacos / RabbitMQ Sync Tool Native Windows", AppMetadata.ProductName);
```

- [ ] **Step 4: 运行依赖与元数据测试**

```powershell
dotnet test native/windows/NacosSyncTool.Windows.sln --filter "FullyQualifiedName~AppMetadataTests|FullyQualifiedName~RabbitMqDependencyGraphTests"
```

Expected: PASS，能够从 `App.ServiceProvider` 解析 `ShellViewModel` 和 `RabbitMqModuleViewModel`。

- [ ] **Step 5: Commit**

```powershell
git add native/windows/src/NacosSyncTool.Windows/App.xaml.cs native/windows/src/NacosSyncTool.Windows/Models/AppMetadata.cs native/windows/src/NacosSyncTool.Windows/MainWindow.xaml native/windows/tests/NacosSyncTool.Windows.Tests
git commit -m "feat: wire RabbitMQ native module"
```

---

### Task 16: 全量回归、安全审计和便携包验证

**Files:**
- Modify: `native/windows/README.md`
- Modify: `README.md`
- Verify: `native/windows/src/NacosSyncTool.Windows/`
- Verify: `native/windows/tests/NacosSyncTool.Windows.Tests/`

- [ ] **Step 1: 运行全量测试**

Run:

```powershell
dotnet test native/windows/NacosSyncTool.Windows.sln --configuration Release --verbosity normal
```

Expected: exit code `0`，Nacos 与 RabbitMQ 全部测试通过，无 skipped failure。

- [ ] **Step 2: 执行 DELETE 能力静态审计**

Run:

```powershell
rg -n "DeleteAsync|HttpMethod.Delete|DELETE" native/windows/src/NacosSyncTool.Windows
```

Expected: 生产代码中的 HTTP DELETE 仅出现在 `RabbitMqManagementClient` 的 Queue `/contents` 白名单实现和相关说明中；不存在资源删除方法、命令或 UI 文本。

- [ ] **Step 3: 执行敏感信息静态审计**

Run:

```powershell
rg -n "Password|Authorization" native/windows/src/NacosSyncTool.Windows/Modules/RabbitMq
```

逐项确认：密码仅存在于连接设置、PasswordBox 事件和 DPAPI 处理；日志和异常格式化代码不拼接密码或 Authorization Header。

- [ ] **Step 4: 确认 Electron/Vue 版本未被修改**

Run:

```powershell
git diff --name-only b03964e..HEAD | rg '^src/'
```

Expected: 无输出。RabbitMQ 功能只修改 `native/windows/` 和相关文档。

- [ ] **Step 5: Release 构建和便携包**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File native/windows/build.ps1 -Configuration Release -Portable
```

Expected: 测试、x64、ARM64 发布和压缩均成功；至少确认：

```text
dist/windows-native/win-x64/Nacos.Sync.Tool.Native.Windows.exe
dist/windows-native/NacosSyncTool-Windows-x64-v1.0.2.zip
```

- [ ] **Step 6: 手工验收 Nacos 回归**

启动 x64 便携版，确认：

- 默认进入 Nacos。
- 现有源/目标连接、Namespace 下拉框、文件加载、Key 扫描和同步命令仍可用。
- 切换 RabbitMQ 再返回，Nacos 已填写内容和列表不丢失。

- [ ] **Step 7: 手工验收 RabbitMQ 拓扑同步**

使用测试 RabbitMQ 3.x/4.x 环境验证：连接、同名 Virtual Host、资源加载、预览、只补缺失项、重复执行全部跳过、目标同名配置不同仍跳过、没有任何资源删除入口。

- [ ] **Step 8: 手工验收 Queue 消息清理**

准备包含 Ready 和 Unacked 消息的测试 Virtual Host，确认：

- 页面统计分别显示 Ready 和 Unacked。
- 只出现一次普通确认，不要求输入 Virtual Host。
- 清理后 Ready 为 0 或仅剩清理期间新写入消息。
- Unacked 和消费者连接保持存在。
- 取消时仅停止后续 Queue，不恢复已清理消息。
- 所有网络 DELETE 均为 Queue `/contents`。

- [ ] **Step 9: 文档和最终 Commit**

README 只补充实际完成且已验证的使用说明、Management API 前置条件和 Ready/Unacked 限制。

```powershell
git diff --check
git status --short
git add README.md native/windows/README.md
git commit -m "docs: document RabbitMQ native module"
```
