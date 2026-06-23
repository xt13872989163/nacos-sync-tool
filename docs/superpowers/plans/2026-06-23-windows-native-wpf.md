# Nacos Sync Tool 原生 Windows 版（WPF）实施计划

> **For agentic workers:** 本地无 .NET SDK，所有编译验证依赖 GitHub Actions。Phase 1 优先打通云端构建，产出可运行的 native Windows exe。

**Goal:** 在保留现有 Electron 双平台版本的前提下，新增一个 C# / .NET 10 / WPF 的纯 Windows 原生版，并在 GitHub Actions 上完成构建与 Release 上传。

**Architecture:** MVVM。Services 层封装 Nacos API / 同步 / 配置解析 / 加密存储 / 日志，ViewModels 暴露状态给 WPF View。所有服务用接口隔离，便于单元测试（xUnit + Moq）。

**Tech Stack:** C# / .NET 10 LTS / WPF / CommunityToolkit.Mvvm / HttpClient / YamlDotNet / System.Text.Json / Windows DPAPI / Serilog

---

## 目录结构

```
native/windows/
  NacosSyncTool.Windows.sln
  src/NacosSyncTool.Windows/
    NacosSyncTool.Windows.csproj
    App.xaml / App.xaml.cs
    MainWindow.xaml / MainWindow.xaml.cs
    Models/            # 数据模型
    ViewModels/        # MVVM 状态
    Views/             # 用户控件 / 弹窗
    Services/          # NacosClient / SyncService / ConfigParser / SettingsStore / LogService / DialogService
    Infrastructure/    # DPAPI 加密、HttpClient 工厂等
  tests/NacosSyncTool.Windows.Tests/
    NacosSyncTool.Windows.Tests.csproj
```

现有 `src/`（Electron）、`package.json`、`.github/workflows/release-build.yml` 不动。

---

## Phase 1：打通 GitHub Actions 构建（当前阶段）

目标：在 GitHub 上构建出一个能启动的 WPF 单文件 exe，并在 Release 上传。功能只放一个占位主窗口，证明工具链通了。

### Task 1：创建解决方案与项目骨架

- 创建 `native/windows/NacosSyncTool.Windows.sln`
- 创建 `src/NacosSyncTool.Windows/NacosSyncTool.Windows.csproj`（net10.0-windows，OutputType WinExe，UseWPF）
- 创建 `App.xaml` / `App.xaml.cs`
- 创建 `MainWindow.xaml` / `MainWindow.xaml.cs`（占位文字）
- 创建 `tests/NacosSyncTool.Windows.Tests/NacosSyncTool.Windows.Tests.csproj`（xUnit 占位）

### Task 2：本地提交

- `git add native/`
- `git commit -m "feat(native): scaffold WPF windows native project"`

### Task 3：扩展 GitHub Actions 发布 workflow

- 修改 `.github/workflows/release-build.yml`
- 保留现有 Electron Windows / macOS matrix job
- 新增 `native-windows` job，runs-on windows-2022
- 步骤：checkout → setup-dotnet 10.0.x → dotnet restore → dotnet build → dotnet test → dotnet publish -r win-x64 --self-contained / PublishSingleFile → rename exe → zip → upload-artifact
- `release` job 同时依赖 `package` 和 `native-windows`，tag 发布时把三个版本的 artifact 一起上传到同一个 GitHub Release

### Task 4：推送并验证

- push 到 codex 分支
- 等待 workflow 通过，下载 artifact 验证 exe 存在

---

## Phase 2：核心服务迁移

迁移 Electron 版的核心逻辑到 C#，先做可单测的纯逻辑：

- `ConfigParser`：JSON / YAML / properties 的 Key 查找与 upsert，对齐 `src/main/services/configParser.ts`
- `NacosClient`：login / namespaces / listConfigs / getConfig / publishConfig，对齐 `nacosClient.ts`，表单编码 + accessToken
- `SyncService`：Namespace / File / Key 三种同步，对齐 `syncService.ts`，复用 confirm handler
- 单元测试覆盖上述三类，迁移 `tests/unit/*` 对应用例

## Phase 3：主界面与同步流程

- `MainViewModel`：源/目标连接、Namespace 下拉、同步模式、文件/Key 列表、busy 状态、日志
- `ConnectionPanel` 控件、`DataTable` 列表、`LogPanel`、`ConfirmDialog`
- 接通 IPC 等价的本地调用链

## Phase 4：加密存储 / 日志目录 / Release 上传

- `SettingsStore` + DPAPI 加密
- `LogService`（Serilog）写 runtime / sync / error 日志，默认 `D:\NacosSyncTool\logs`
- 日志目录选择对话框
- 在 `release-build.yml` 的 release job 里上传 native exe / zip 到 Release

---

## 最终产物

```
Electron Windows  : Nacos.Sync.Tool.<ver>.exe          (保留)
Electron macOS    : Nacos.Sync.Tool.<ver>.dmg / mac.zip (保留)
原生 Windows      : Nacos.Sync.Tool.Native.Windows.<ver>.exe (新增)
```
