# RabbitMQ 原生 Windows 同步模块设计

## 1. 目标

在现有原生 Windows WPF 应用中增加独立的 RabbitMQ 同步模块，并保留现有 Nacos 模块的功能、状态和交互。

RabbitMQ 模块通过 RabbitMQ Management HTTP API，在两个 RabbitMQ 集群之间同步同名 Virtual Host 下缺失的拓扑资源，并提供独立的 Virtual Host Queue 消息清理功能。拓扑同步遵循“只补充、不覆盖、不删除”的原则，不迁移消息，不修改源端，也不调整目标端已有资源；消息清理仅清除用户明确选择的 Virtual Host 下各 Queue 的 Ready 消息。

本设计仅覆盖 `native/windows/` 原生 Windows 版本。Electron/Vue 版本不在本次范围内。

## 2. 功能范围

### 2.1 包含范围

- 在同一个 WPF 程序内切换 Nacos 和 RabbitMQ 模块。
- 使用 Management HTTP API 连接源 RabbitMQ 和目标 RabbitMQ。
- 读取源端可访问的 Virtual Host。
- 同步同名 Virtual Host。
- 同步 Exchange、Queue、Binding 和 Policy。
- 支持整套 Virtual Host 同步和选择性同步。
- 在执行前生成同步预览。
- 保存 RabbitMQ 最近一次连接配置，并使用 Windows DPAPI 加密密码。
- 提供独立的 RabbitMQ 日志、执行结果和失败项重试。
- 提供独立的“队列消息清理”入口，一次批量清空所选 Virtual Host 下所有可访问 Queue 的 Ready 消息。

### 2.2 不包含范围

- 不通过 AMQP `5672` 端口实现资源发现或同步。
- 不迁移 Queue 中的消息。
- 不清除已经投递给消费者但尚未确认的 Unacked 消息，不主动断开消费者。
- 不同步用户、角色、权限、Global Parameter、Shovel、Federation Upstream 等管理资源。
- 不支持把源 Virtual Host 映射到不同名称的目标 Virtual Host。
- 不更新、覆盖或删除目标端已有资源。
- 不修改 Electron/Vue 版本。
- 第一版不提供多套 RabbitMQ 连接配置档案管理。

## 3. 前置条件

- 源端和目标端 RabbitMQ 均启用 `rabbitmq_management` 插件。
- 当前 Windows 电脑能够访问两端的 Management HTTP API。
- 用户账号具备读取源端资源所需权限。
- 目标端账号具备创建 Virtual Host、Exchange、Queue、Binding 和 Policy 所需权限。
- 消息清理操作端账号具备读取 Queue 和 Purge Queue 消息所需权限。
- Management API 地址允许使用默认端口、自定义端口、反向代理路径或 HTTPS 地址。

## 4. 总体架构

采用“同一程序、两个独立业务模块”的结构。主窗口负责应用级导航和主题，Nacos 与 RabbitMQ 分别维护自己的界面、状态、模型和服务。

```text
原生同步工具
├─ 应用外壳
│  ├─ Nacos / RabbitMQ 模块切换
│  └─ 主题与公共窗口能力
├─ Nacos 模块
│  ├─ 原有连接与 Namespace 选择
│  ├─ Namespace / 文件 / Key 同步
│  └─ 独立状态与日志
└─ RabbitMQ 模块
   ├─ 源端和目标端 Management API 连接
   ├─ Virtual Host 与资源加载
   ├─ 同步计划与预览
   ├─ 同步执行
   ├─ Queue Ready 消息批量清理
   └─ 独立状态与日志
```

推荐目录边界：

```text
native/windows/src/NacosSyncTool.Windows/
├─ Modules/
│  ├─ Nacos/
│  │  ├─ Views/
│  │  ├─ ViewModels/
│  │  ├─ Models/
│  │  └─ Services/
│  └─ RabbitMq/
│     ├─ Views/
│     ├─ ViewModels/
│     ├─ Models/
│     └─ Services/
└─ Shared/
   ├─ Logging/
   ├─ Dialogs/
   ├─ Themes/
   └─ CommonModels/
```

### 4.1 模块隔离规则

- 保留现有 Nacos 业务行为，仅进行模块化移动和应用外壳接入所需的调整。
- RabbitMQ 模块不得调用或复用 `NacosApiService`、Nacos `SyncService` 和 Nacos 资源模型。
- 两个模块分别保存连接信息、连接状态、当前选择、列表数据、预览和日志。
- 切换模块不会断开连接，也不会清除另一个模块的状态。
- RabbitMQ 异常不能改变 Nacos 模块状态。
- 公共代码只包含主题、基础日志能力、对话框基础设施和无业务含义的通用模型。
- RabbitMQ 模块内部的“拓扑同步”和“队列消息清理”使用独立操作状态，清理操作不复用源端或目标端的业务含义。

## 5. RabbitMQ 连接模型

拓扑同步的源端、目标端以及消息清理的独立操作端都包含以下配置：

- Management API 基础地址。
- 用户名。
- 密码。
- 请求超时。
- HTTPS 证书校验设置；默认启用严格校验。

测试连接时读取并显示：

- RabbitMQ 版本。
- 集群名称。
- 当前账号可访问的 Virtual Host。
- 连接失败时的 HTTP 状态和明确错误原因。

源端客户端只允许发出读取请求。目标端客户端允许读取和创建请求。消息清理操作端客户端允许读取，并且只允许 Queue `/contents` Purge 请求。三种连接角色必须使用独立客户端实例，禁止共享可变的地址或认证状态。

## 6. 资源模型

### 6.1 Virtual Host

保存名称，以及 Management API 能够提供的描述、标签和默认队列类型。源端选择哪个 Virtual Host，目标端就使用相同名称；目标不存在时创建，存在时跳过创建。

### 6.2 Exchange

保存以下核心字段：

- 名称。
- 类型。
- `durable`。
- `auto_delete`。
- `internal`。
- `arguments`。

默认 Exchange 和 RabbitMQ 内置的 `amq.*` Exchange 不进入同步计划。

### 6.3 Queue

保存以下核心字段：

- 名称。
- `durable`。
- `auto_delete`。
- 队列类型和其他 `arguments`。

临时生成的 `amq.gen-*` Queue 和 Exclusive Queue 不进入同步计划。Quorum Queue、Stream Queue 等类型保留源端参数，不自动转换或降级。

### 6.4 Binding

保存以下字段：

- 来源 Exchange。
- 目标名称。
- 目标类型：Exchange 或 Queue。
- Routing Key。
- `arguments`。

Binding 没有独立业务名称，其存在性使用“来源 Exchange + 目标名称 + 目标类型 + Routing Key”组合判断。按照已确认的安全规则，组合字段相同即视为已存在，不继续比较 `arguments`。

### 6.5 Policy

保存以下字段：

- 名称。
- 匹配表达式 Pattern。
- Definition。
- Priority。
- Apply To。

Policy 内容原样提交到目标端。目标端不支持某个 Definition 时，该 Policy 记录失败，不进行字段删除、转换或降级。

## 7. 目标存在性和写入规则

同步只创建目标端缺失的资源。

- Virtual Host：按名称判断。
- Exchange：在目标 Virtual Host 内按名称判断。
- Queue：在目标 Virtual Host 内按名称判断。
- Policy：在目标 Virtual Host 内按名称判断。
- Binding：按来源 Exchange、目标名称、目标类型和 Routing Key 判断。

目标资源存在时：

- 不比较其他配置。
- 不更新或覆盖。
- 不删除重建。
- 在预览和结果中标记为“目标已存在，跳过”。

拓扑同步不得提供删除或覆盖入口，也不得发送任何 `DELETE` 请求。只有独立的 Queue 消息清理服务可以发送严格受限的 `/api/queues/{vhost}/{queue}/contents` 请求，该请求只清除 Ready 消息，不删除 Queue 本身。

## 8. 依赖关系与执行顺序

执行顺序固定为：

```text
Virtual Host → Exchange → Policy → Queue → Binding
```

规则如下：

- Virtual Host 创建失败时，该 Virtual Host 下的所有资源均标记为依赖失败。
- Exchange 先于 Queue 创建，以便死信、备用交换等引用尽快具备目标资源。
- Policy 在 Queue 之前创建，使新建 Queue 能立即应用匹配策略。
- Binding 只有在来源和目标资源已存在或本次已成功创建时才执行。
- 选择性同步 Binding 时只检查依赖，不自动创建用户未选择的 Exchange 或 Queue。
- 单项失败不会阻止其他无依赖资源继续执行。

## 9. 同步模式

### 9.1 整套 Virtual Host 同步

- 加载源 Virtual Host 下全部可同步资源。
- 自动过滤内置或不支持的资源。
- 默认选择剩余全部资源。
- 目标端使用同名 Virtual Host。
- 生成预览并确认后执行。

### 9.2 选择性同步

- 分别在 Exchange、Queue、Binding 和 Policy 页签选择资源。
- 支持搜索、排序、全选、反选和清空选择。
- 用户修改选择后，旧同步预览立即失效。
- 用户选择 Binding 但未选择且目标也不存在其依赖资源时，Binding 标记为依赖缺失。

## 10. 同步预览

任何写入前必须先生成不可变的同步计划。计划基于当前源端资源快照和目标端状态，不能边执行边重新改变选中范围。

每项资源具有以下计划状态之一：

- `待创建`。
- `目标已存在，跳过`。
- `不支持同步`。
- `依赖缺失`。
- `无法判断`。

预览显示：

- 每种资源的选中数量。
- 待创建数量。
- 已存在跳过数量。
- 不支持数量。
- 依赖缺失或无法判断数量。
- 每个异常项的具体原因。

修改连接、切换 Virtual Host、刷新资源或改变选择后，当前同步计划立即失效，必须重新生成。

## 11. 同步执行

执行前展示计划摘要并要求用户确认。由于本模块不覆盖、不删除目标资源，不需要 Nacos Namespace 覆盖场景中的双重确认，但必须保留一次明确的执行确认。

执行规则：

- 同一时间只允许一个 RabbitMQ 同步任务。
- 默认顺序执行，避免对管理节点造成突发请求压力。
- 每次创建前再次按已定义的存在性规则检查目标状态。
- 如果资源在预览后被其他操作创建，重新检查确认存在后标记为“并发创建，已跳过”。
- 网络超时和临时服务器错误最多自动重试两次。
- 认证失败、权限不足和参数不支持不自动重试。
- 用户可以取消任务；取消只停止后续资源，不回滚已成功创建的资源。
- 执行完成后允许只对失败项重新生成计划并重试。

最终汇总包括：

- 创建成功数量。
- 已存在跳过数量。
- 不支持数量。
- 依赖缺失数量。
- 创建失败数量。
- 用户取消前已处理数量。

使用相同源端重复同步时，只会补充仍然缺失的资源，流程应保持幂等。

## 12. 队列消息清理

### 12.1 功能入口与连接

RabbitMQ 模块内部提供“拓扑同步”和“队列消息清理”两个独立操作入口。消息清理页面使用独立的 RabbitMQ 操作端连接，不使用拓扑同步页面的源端或目标端概念。

连接成功后，用户选择一个 Virtual Host。页面读取并展示该 Virtual Host 下全部可访问 Queue，至少包括：

- Queue 名称。
- Ready 消息数量。
- Unacked 消息数量。
- 消费者数量。
- 消息总数。

Queue 列表为只读列表。该功能只提供“刷新”和“一键清空 Ready 消息”，不提供逐个 Queue 选择，以确保“一键清空当前 Virtual Host”的语义明确。

### 12.2 清理语义

- 对当前 Virtual Host 下每个可访问 Queue 调用 Management API 的 Queue Purge 接口。
- 只清除 Ready 状态、尚未投递给消费者的消息。
- 不清除已经投递但尚未确认的 Unacked 消息。
- 不主动断开消费者，也不通过删除重建 Queue 实现清理。
- 不修改 Queue、Exchange、Binding、Policy 或 Virtual Host 配置。
- 包括临时 Queue 和 Exclusive Queue；无法清除时记录单项失败。
- 清理期间生产者新写入的消息可能在操作结束后仍然存在。
- 操作开始后新创建的 Queue 不属于本次固定快照。
- Management API 消息统计可能存在短暂延迟，最终状态以执行完成后的刷新结果为准。

RabbitMQ Management API 没有清空整个 Virtual Host 的单一接口。工具对用户表现为一次批量操作，内部按 Queue 顺序调用：

```text
DELETE /api/queues/{vhost}/{queue}/contents
```

### 12.3 确认流程

点击“一键清空 Ready 消息”后，工具立即重新读取当前 Virtual Host 的 Queue 列表和消息统计，再显示一次普通确认对话框。

确认对话框展示：

- RabbitMQ 地址和集群名称。
- 当前 Virtual Host。
- Queue 数量。
- 预计可清除的 Ready 消息总数。
- Unacked 消息数量，并明确说明其不会被清除。
- “操作不可恢复，生产者仍可能继续写入”的警告。

对话框仅提供“确认清空”和“取消”按钮，默认焦点位于“取消”。不要求用户输入 Virtual Host 名称，也不增加第二次确认。

### 12.4 执行与取消

用户确认后固定本次 Queue 快照，并按顺序逐个清理：

- 同一时间只允许一个消息清理任务。
- 显示当前 Queue、总进度、成功数、失败数和清理前 Ready 数量。
- 网络超时和临时 `5xx` 最多自动重试两次。
- `401 Unauthorized` 或 `403 Forbidden` 立即停止整个任务。
- Queue 被并发删除导致 `404 Not Found` 时标记为“Queue 已不存在”并继续。
- 单个 Queue 的其他错误记录失败，但不阻止后续 Queue。
- 用户可以取消任务；取消只停止后续 Queue，已经清除的消息无法恢复。
- 完成或取消后重新加载 Queue 状态。

最终汇总包括：

- Queue 总数。
- 清理成功数量。
- Queue 已不存在数量。
- 清理失败数量。
- 取消前已处理数量。
- 各 Queue 清理前的 Ready 数量。
- 刷新后剩余的 Ready 和 Unacked 数量。

### 12.5 DELETE 安全边界

- 只有 `RabbitMqQueuePurgeService` 可以调用 DELETE。
- DELETE 请求路径必须严格匹配 `/api/queues/{vhost}/{queue}/contents`。
- HTTP 客户端在发送前必须执行路径白名单校验。
- 禁止 DELETE Virtual Host、Queue 本身、Exchange、Binding、Policy 或其他 RabbitMQ 资源。
- 拓扑同步相关客户端和执行器继续保持完全无 DELETE 权限。
- 日志记录用户确认、RabbitMQ 地址、Virtual Host、Queue、清理前 Ready 数量和结果，但不得记录密码或认证 Header。

## 13. 错误处理

- `401 Unauthorized`：提示用户名或密码无效。
- `403 Forbidden`：提示账号缺少读取、创建或 Queue Purge 权限，并展示当前操作和对应资源类型。
- `404 Not Found`：区分 API 路径错误、Virtual Host 不存在和资源不存在。
- `409 Conflict` 或等价声明冲突：重新读取目标同名资源；存在则记为跳过，否则记为失败。
- 请求超时或临时 `5xx`：按重试规则处理。
- JSON 字段缺失或版本差异：记录具体字段和响应上下文，不使整个应用崩溃。
- 目标版本不支持源端参数：保留原始失败信息，不修改参数后重试。
- Virtual Host 名称和资源名称在 URL 路径中严格编码，特别处理默认 `/` Virtual Host。

日志不得包含密码、Authorization Header 或其他认证密钥。

## 14. 界面设计

主窗口顶部增加明确的 Nacos / RabbitMQ 模块切换控件。Nacos 模块保持现有布局，RabbitMQ 模块使用相同主题体系，并在模块内部提供“拓扑同步 / 队列消息清理”两个独立操作入口。

```text
┌──────────────────────────────────────────────────────────┐
│ 原生同步工具        [ Nacos ] [ RabbitMQ ]       主题设置 │
├──────────────────────────────────────────────────────────┤
│ RabbitMQ 操作：[拓扑同步] [队列消息清理]                 │
├──────────────────────────────────────────────────────────┤
│ 源 RabbitMQ                          目标 RabbitMQ        │
│ 管理地址 / 用户名 / 密码              管理地址 / 用户名 / 密码 │
│ [测试连接]  集群和版本                 [测试连接]  集群和版本 │
│                                                          │
│ 源 Virtual Host: /dev                  目标: /dev          │
│                                        状态: 已存在/待创建  │
├──────────────────────────────────────────────────────────┤
│ [整套同步] [选择性同步]   [刷新资源] [生成同步预览]        │
├──────────────────────────────────────────────────────────┤
│ [Exchange] [Queue] [Binding] [Policy]                    │
│ ┌──────────────────────────────────────────────────────┐ │
│ │ 选择  名称/目标  类型  关键配置  目标状态  说明       │ │
│ └──────────────────────────────────────────────────────┘ │
├──────────────────────────────────────────────────────────┤
│ 计划：待创建 12｜跳过 5｜异常 1     [执行同步] [取消]     │
├──────────────────────────────────────────────────────────┤
│ RabbitMQ 操作日志                                        │
└──────────────────────────────────────────────────────────┘
```

交互规则：

- 使用固定模块切换按钮，不使用下拉框。
- RabbitMQ 内部使用固定的“拓扑同步 / 队列消息清理”切换按钮。
- 目标 Virtual Host 名称只读并始终与源端相同。
- 未生成有效预览时禁用“执行同步”。
- 执行期间锁定连接信息、Virtual Host 和资源选择。
- 执行期间允许取消后续操作。
- 每个资源页签显示选中数、待创建数、跳过数和异常数。
- 状态同时使用文字和图标，不只依赖颜色。
- 长 `arguments` 和 Policy Definition 以摘要展示，可打开格式化 JSON 详情。
- 表格支持键盘操作、搜索、排序和复制资源名称。
- Nacos 页面只显示 Nacos 日志，RabbitMQ 页面只显示 RabbitMQ 日志。
- 队列消息清理页面使用独立操作端连接、只读 Queue 列表和醒目的危险操作按钮。
- 清理确认对话框只确认一次，不要求输入 Virtual Host 名称，默认焦点放在取消按钮。

## 15. 服务与模型边界

RabbitMQ 模块的主要代码结构：

```text
Modules/RabbitMq/
├─ Views/
│  └─ RabbitMqModuleView.xaml
├─ ViewModels/
│  └─ RabbitMqModuleViewModel.cs
├─ Models/
│  ├─ RabbitMqConnection.cs
│  ├─ RabbitMqVirtualHost.cs
│  ├─ RabbitMqExchange.cs
│  ├─ RabbitMqQueue.cs
│  ├─ RabbitMqBinding.cs
│  ├─ RabbitMqPolicy.cs
│  ├─ RabbitMqSyncPlan.cs
│  └─ RabbitMqQueuePurgeResult.cs
└─ Services/
   ├─ RabbitMqManagementClient.cs
   ├─ RabbitMqResourceLoader.cs
   ├─ RabbitMqPlanBuilder.cs
   ├─ RabbitMqSyncExecutor.cs
   ├─ RabbitMqQueuePurgeService.cs
   └─ RabbitMqSettingsStore.cs
```

职责：

- `RabbitMqManagementClient`：处理 Management API 请求、认证、URL 编码和响应转换。
- `RabbitMqResourceLoader`：加载、过滤、规范化和快照化资源。
- `RabbitMqPlanBuilder`：比较源端与目标端状态并生成同步计划，不执行写入。
- `RabbitMqSyncExecutor`：按照计划处理创建、重试、取消、依赖和汇总。
- `RabbitMqQueuePurgeService`：加载 Queue 快照、执行确认后的 Ready 消息清理、限制 DELETE 路径并汇总结果。
- `RabbitMqSettingsStore`：保存独立配置并使用 DPAPI 加密敏感字段。
- `RabbitMqModuleViewModel`：编排界面状态和命令，不直接拼装 HTTP 请求。

## 16. 配置与日志隔离

- Nacos 和 RabbitMQ 使用不同的配置分区或不同存储键。
- RabbitMQ 保存源端、目标端和消息清理操作端的地址、用户名、加密密码、请求超时及最近选择的 Virtual Host。三种连接角色使用不同存储键。
- 清除 RabbitMQ 设置不能影响 Nacos 设置。
- RabbitMQ 和 Nacos 分别维护可观察日志集合。
- 公共日志基础设施可以复用格式和落盘能力，但必须包含模块标识并在界面中分流。
- RabbitMQ 日志条目至少包含时间、级别、资源类型、资源名称和执行结果。

## 17. 版本兼容策略

- 连接时记录源端和目标端 RabbitMQ 版本。
- JSON 解析忽略未知字段，兼容 Management API 字段扩展。
- 创建请求只发送对应资源所需字段。
- 不假设源端和目标端版本完全一致。
- 不为版本差异自动删除或修改源参数。
- 集成验证至少覆盖一个 RabbitMQ 3.x 环境和一个 RabbitMQ 4.x 环境。

## 18. 测试方案

### 18.1 单元测试

- Management API 地址规范化。
- Basic Authentication 和敏感日志过滤。
- 请求超时和取消。
- Virtual Host 及资源名称 URL 编码。
- 内置 Exchange、临时 Queue、Exclusive Queue 过滤。
- Exchange、Queue、Policy 按名称跳过。
- 同名但配置不同的资源仍然跳过。
- Binding 组合字段存在性判断。
- 同步计划顺序与依赖缺失判断。
- 网络异常重试和不可重试错误分类。
- 单项失败后继续执行无依赖资源。
- 源端客户端不允许写入请求。
- 拓扑同步执行器不产生 `DELETE` 请求。
- Queue Purge DELETE 路径白名单拒绝任何非 `/contents` 删除请求。
- Ready 和 Unacked 消息数量分别统计，预计清除数量只包含 Ready。
- 清理确认被取消时不发送 Purge 请求。
- Queue 顺序清理、自动重试、中途取消和单项失败继续。
- 清理操作端与拓扑同步源端、目标端配置隔离。
- RabbitMQ 与 Nacos 配置和日志隔离。

### 18.2 API 合约测试

通过可控的 HTTP Handler 或测试服务器验证：

- 连接测试和版本读取。
- Virtual Host、Exchange、Queue、Binding、Policy 的读取与创建请求。
- 默认 `/` Virtual Host 的路径编码。
- `401`、`403`、`404`、冲突、超时和 `5xx` 处理。
- 创建前二次存在性检查。
- 取消任务后不再发送后续创建请求。
- Queue Purge 使用正确编码的 `/api/queues/{vhost}/{queue}/contents` 请求。
- Purge 遇到 `401/403` 停止任务，遇到 Queue `404` 记录后继续。
- 清理取消后不再发送后续 Queue Purge 请求。

### 18.3 集成与回归测试

- 使用 RabbitMQ 3.x 和 4.x 测试环境进行整套同步。
- 验证重复执行不会修改或删除目标已有资源。
- 验证源端没有任何写入。
- 验证选择性同步及 Binding 依赖缺失提示。
- 验证失败项重试。
- 验证 Virtual Host 内所有可访问 Queue 的 Ready 消息批量清理。
- 验证 Unacked 消息和消费者连接不受清理操作影响。
- 验证清理后刷新、执行中取消和部分 Queue 失败汇总。
- 运行现有 Nacos 单元测试和原生版构建。
- 手工回归 Nacos 连接、资源加载和三种同步模式。
- 验证 Windows x64 便携包能够启动并切换模块。

## 19. 验收标准

- 用户能够在 Nacos 和 RabbitMQ 模块之间切换，两个模块状态互不影响。
- 能连接两个启用了 Management API 的 RabbitMQ 集群。
- 能加载指定源 Virtual Host 的 Exchange、Queue、Binding 和 Policy。
- 能准确生成待创建、已存在跳过、不支持和依赖缺失预览。
- 能同步整个同名 Virtual Host 或用户选择的资源。
- 目标端已有同名资源时不比较配置且不做修改。
- 重复同步只补充缺失资源。
- 工具不会迁移消息、删除拓扑资源、覆盖目标资源或在拓扑同步中写入源端。
- 用户能够通过独立入口连接 RabbitMQ，选择 Virtual Host，并在一次明确确认后批量清除所有可访问 Queue 的 Ready 消息。
- 消息清理不会清除 Unacked 消息，不会断开消费者，也不会删除 Queue。
- 除 Queue `/contents` Purge 接口外，工具不会发送任何 DELETE 请求。
- RabbitMQ 单项失败不会使无依赖的其他资源停止同步。
- Nacos 现有功能和测试保持正常。
- 能生成可运行的 Windows x64 便携版本。

## 20. 实施约束

- 实施前先编写独立的详细实施计划。
- 优先通过小型、职责明确的类实现，不将 RabbitMQ 逻辑继续堆入现有 `MainViewModel`。
- 模块化移动 Nacos 代码时不得顺带改变其同步行为。
- 所有拓扑同步目标写操作必须由同步计划驱动，界面层不能直接调用创建 API；消息清理只能通过独立清理服务执行。
- Queue 消息清理必须由独立服务执行，不能复用或绕过拓扑同步计划与执行器的安全边界。
- 任何会引入覆盖、删除拓扑资源、迁移消息、清理 Unacked 消息或跨名称 Virtual Host 映射的需求都属于后续独立设计范围。
