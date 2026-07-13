# RabbitMQ 原生模块完善与安全加固设计

## 1. 目标

在不改变 Nacos 现有行为、不增加 RabbitMQ 拓扑删除能力的前提下，修复当前 RabbitMQ 原生 Windows 模块中已确认的界面、连接状态、执行状态和 HTTP 安全问题，使用户能够稳定完成以下闭环：

1. 从 Nacos 切换到 RabbitMQ 并正常显示页面。
2. 连接源端、目标端或消息清理端后，界面输入与实际客户端保持一致。
3. 连接信息变化后，任何旧快照、旧计划和旧客户端立即失效。
4. 拓扑同步只创建缺失资源，不覆盖、不更新、不删除。
5. 消息清理只调用 Queue `/contents` Purge，且确认内容与实际连接一致。
6. 网络、配置和取消异常能够被界面捕获并给出准确结果。

## 2. 不在本次范围内

- 不修改 Electron/Vue 版本。
- 不改变 Nacos 同步流程、覆盖规则或界面布局。
- 不支持 5672 AMQP 端口进行拓扑同步。
- 不增加跨名称 Virtual Host 映射。
- 不增加删除 Virtual Host、Exchange、Queue、Binding、Policy 的入口或 API。
- 不清理 Unacked 消息，不断开消费者。
- 不新增 RabbitMQ 消息迁移、消费或发布功能。

## 3. 已确认根因

### 3.1 模块显示

`RabbitMqModuleView` 在同一元素上设置 RabbitMQ ViewModel 作为 `DataContext`，同时直接绑定只存在于 Shell ViewModel 的 `IsRabbitMqSelected`，导致 Visibility 在错误对象上解析并保持折叠。

### 3.2 连接与输入脱节

Management API 地址、用户名和密码是可编辑字段，但修改后不会清理已经创建的客户端。确认框读取当前输入值，执行服务却继续使用旧客户端，因此可能出现界面显示集群 B、实际操作集群 A 的情况。

连接重试还会在验证成功前覆盖旧客户端；失败后旧状态、列表和同步计划可能继续保留。旧客户端也没有被释放。

### 3.3 计划和结果状态不完整

连接变化不会废弃旧同步计划。执行汇总只统计 `Failed`，遗漏 `Missing`、`Cancelled` 和未处理项，并无条件写成功日志。“重试失败项”实际重新执行当前全部选择，名称与行为不一致。

### 3.4 异常边界不完整

Queue 清理预览和模块初始化位于统一异常处理之外。网络异常、损坏 JSON 或不可解密的 DPAPI 数据可能直接进入 WPF Dispatcher 异常路径。

### 3.5 HTTP 安全边界

Queue Purge 在发送前只校验初始相对路径。默认 HTTP 自动重定向可能让 DELETE 最终落到未经白名单验证的地址。点段名称、URL 内嵌凭据、query 和 fragment 也没有显式拒绝。

日志敏感字段正则无法完整遮盖标准 `Authorization: Basic token` 和 JSON 形式密码。

## 4. 设计方案

### 4.1 Shell 与页面显示

- RabbitMQ 根视图的 Visibility 明确通过 `RelativeSource AncestorType=Window` 绑定 Shell ViewModel。
- Nacos 根视图保持现有绑定和布局不变。
- 增加回归测试，明确检查 RabbitMQ Visibility 的绑定源来自 Window，而不是 RabbitMQ ViewModel。

### 4.2 连接会话与生命周期

每个角色继续使用独立连接：

- `SourceReadOnly`
- `TargetTopology`
- `QueuePurge`

连接流程调整为：

1. 将当前角色标记为未连接，清理旧状态并释放旧客户端。
2. 根据当前输入创建候选客户端。
3. 使用候选客户端完成 Overview 和 Virtual Host 验证。
4. 全部成功后，保存候选客户端和不可变的已连接信息。
5. 更新列表、选择项和加密设置。

验证失败时释放候选客户端，当前角色保持未连接，禁止继续执行读取、同步或清理。

地址、用户名或密码任一发生变化时，对应角色立即执行失效处理：

- 释放旧客户端并设为 `null`。
- 清除连接成功标识和连接摘要。
- 清除由该连接加载的 Virtual Host、Queue 或资源快照。
- 源端或目标端变化时清除当前同步计划。
- 清理端变化时清除 Queue 列表、统计与清理摘要。

实际确认内容使用验证成功时保存的不可变连接摘要，不再读取可能已编辑但尚未连接的输入字段。

`IRabbitMqManagementClient` 增加明确的释放契约，ViewModel 在替换客户端和应用关闭时释放资源。

### 4.3 计划失效和执行结果

- 源端连接、目标端连接、源 Virtual Host、同步选择发生变化时，当前计划立即失效。
- 生成新计划前先清除旧计划；生成失败后不得保留旧计划供执行。
- 执行器继续在写入前复查目标资源。
- 临时网络错误和 5xx 的重试覆盖存在性检查和创建请求。
- 并发创建导致冲突时重新读取目标端；如果同名资源已经存在，则标记为跳过，否则保留失败。

执行汇总增加：

- 创建数量。
- 跳过数量。
- 失败数量。
- 缺失依赖数量。
- 取消或未处理数量。

日志级别根据结果选择：只有完整执行且没有失败、缺失或取消时记录成功；其他情况记录警告或错误。

现有“重试失败项”按钮改为准确描述实际行为的“重新检查并执行”，重新读取目标状态、重新生成计划并再次要求确认，不声称只处理上次失败集合。

### 4.4 Queue Ready 消息清理

- 预览加载、确认和执行统一置于可观察的命令状态中。
- 预览使用可取消 token；网络错误进入 RabbitMQ 日志，不进入未处理 Dispatcher 异常。
- 清理期间清理页提供可见的取消按钮。
- 清理命令和连接、刷新命令通过 CanExecute 与统一忙碌状态防止重入。
- 取消后明确显示“已取消”和剩余统计，不显示为成功完成。
- Queue 不存在继续处理后续 Queue，但计入 Missing，不再显示为“失败 0”。
- 清理结束后的刷新失败时继续显示“剩余统计未知”，不推断为 0。

确认仍然只有一次，不要求输入 Virtual Host。确认框展示验证成功时固定的集群、Management API 地址、Virtual Host、Queue 数量、Ready 和 Unacked。

### 4.5 Management HTTP API 安全

- 禁用 `HttpClientHandler` 自动重定向。3xx 响应作为错误返回，避免 DELETE 离开白名单路径。
- Management API 基础地址继续允许 HTTP、HTTPS、自定义端口和反向代理路径。
- 拒绝包含 URI userinfo、query 或 fragment 的基础地址，避免明文凭据和路径歧义。
- Purge 发送前校验最终绝对 URI 的 escaped path，而不是只校验拼接前字符串。
- 对 `.`、`..` 等会触发 URI 点段规范化的 Virtual Host 或 Queue 名称采用 fail-closed：拒绝发送并记录明确错误，不允许请求落到其他路径。
- 保持三种客户端角色权限边界；拓扑客户端仍不具备任何 DELETE 能力。

### 4.6 日志和配置恢复

- 日志脱敏覆盖：
  - `password=value`
  - `"password":"value"`
  - `Authorization: Basic token`
  - `authorization=value`
  - URL userinfo
- HTTP 错误正文在写日志前继续经过统一脱敏。
- RabbitMQ 设置文件损坏、JSON 无效或 DPAPI 无法解密时，不让模块初始化失败。
- 无效角色条目被忽略并记录不含密文的警告；其他有效角色继续加载。
- 保持密码只通过 DPAPI `CurrentUser` 加密保存。

### 4.7 表格与密码输入

- Exchange、Queue、Binding、Policy 的资源字段全部只读。
- 仅“选择”复选框允许修改。
- 保存密码加载后，界面提供明确的“已保存密码”状态；不把明文密码反向填入 PasswordBox。
- 用户修改 PasswordBox 时立即替换内存凭据并使连接失效。

## 5. 测试设计

### 5.1 Shell 与 XAML

- RabbitMQ Visibility 使用 Window/Shell 作为绑定源。
- Nacos 与 RabbitMQ 切换状态互斥。
- 资源列只读，选择列可编辑。
- 清理页存在取消入口，不存在任何资源删除按钮或命令。

### 5.2 ViewModel

- 修改源、目标或清理端地址后，旧客户端被释放，对应状态和计划被清除。
- 失败候选连接不会覆盖已验证客户端。
- 目标端重连后旧计划不可执行。
- 清空确认使用已验证连接地址，而不是未连接的输入值。
- 预览失败只记录错误，不调用 Purge。
- 忙碌期间危险命令不能重入。
- 初始化遇到一个损坏角色设置时，其他角色仍可加载。

### 5.3 执行器与结果

- Missing 和 Cancelled 正确进入汇总。
- 存在性检查对 timeout/5xx 重试。
- 并发创建冲突在复查存在后转为 Skipped。
- 重新检查执行先生成新计划，不复用失败的旧计划。

### 5.4 HTTP 安全

- 307/308 不自动跟随。
- userinfo、query、fragment 地址被拒绝。
- 点段名称不发送 DELETE。
- 唯一 DELETE 仍严格是 Queue `/contents`。
- 标准 Basic Header、JSON 密码和 URL userinfo 均被完整脱敏。

## 6. 验证与发布

- 本地不安装 .NET SDK，也不执行本地构建。
- 使用静态审计检查唯一 DELETE、Electron `src/` 无改动、工作区不包含 `artifacts/` 提交。
- 提交并推送到 `codex/nacos-sync-tool-mvp`。
- 以 GitHub Actions 的 Restore、Build、Run tests、x64 Publish、ARM64 Publish 全部成功作为构建验证依据。

## 7. 验收标准

满足以下全部条件后，本轮完善才算完成：

1. 点击 RabbitMQ 后页面正常显示并可返回 Nacos。
2. 输入连接信息与实际客户端不可能脱节。
3. 连接变化后旧计划不能执行。
4. 清理确认展示的集群和地址就是实际接收 Purge 的连接。
5. 任何实际发送的 DELETE 都不能通过重定向或路径规范化离开 Queue `/contents`。
6. 异常、缺失和取消不会被汇总为成功。
7. 拓扑资源字段不可编辑，且不存在资源删除入口。
8. 凭据不会以 Basic token、JSON 值或 URL userinfo 形式写入日志或设置明文字段。
9. GitHub 原生 Windows 工作流全部通过。
