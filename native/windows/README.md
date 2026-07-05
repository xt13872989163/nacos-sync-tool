# Nacos Sync Tool - Windows 原生版本

## 项目说明

这是 Nacos Sync Tool 的 Windows 原生版本，使用 WPF (Windows Presentation Foundation) 和 .NET 10.0 开发。

相比 Electron 版本，原生版本具有以下优势：
- ✅ 更小的安装包体积
- ✅ 更低的内存占用
- ✅ 更好的 Windows 系统集成
- ✅ 更快的启动速度

## 技术栈

- **.NET 10.0** - 最新的 .NET 框架
- **WPF** - Windows Presentation Foundation UI 框架
- **MVVM 架构** - 使用 CommunityToolkit.Mvvm
- **依赖注入** - Microsoft.Extensions.DependencyInjection
- **YamlDotNet** - YAML 配置解析
- **System.Security.Cryptography** - 连接信息加密存储

## 当前开发进度

### ✅ 已完成

#### 1. 基础架构
- [x] 项目结构搭建
- [x] MVVM 架构设置
- [x] 依赖注入容器
- [x] 主题系统（4 个主题：Forest、Slate、Warm、Nexus）

#### 2. 连接配置界面
- [x] 源端 Nacos 连接表单（地址、账号、密码）
- [x] 目标端 Nacos 连接表单（地址、账号、密码）
- [x] 连接测试功能
- [x] Namespace 下拉选择
- [x] 目标端新建 Namespace 功能

#### 3. Nacos API 服务
- [x] 登录认证（支持 Nacos 1.x 和 2.x）
- [x] 获取 Namespace 列表
- [x] 创建 Namespace
- [x] 测试连接功能
- [x] 错误处理和超时控制

#### 4. 日志系统
- [x] 实时日志显示
- [x] 日志级别（Info、Success、Warning、Error）
- [x] 日志颜色标识
- [x] 清空日志功能
- [x] 日志数量限制（防止内存溢出）

#### 5. UI 组件
- [x] 主题切换
- [x] 响应式布局
- [x] 加载状态提示
- [x] 数据绑定
- [x] 命令绑定

### 🚧 待实现

#### 6. 数据展示区
- [ ] 配置文件列表（文件级别同步）
- [ ] Key 扫描结果列表（Key 级别同步）
- [ ] 多选支持
- [ ] 排序和筛选

#### 7. 同步功能
- [ ] Namespace 级别同步
- [ ] 文件级别同步
- [ ] Key 级别同步
- [ ] 双重确认对话框
- [ ] 覆盖/跳过逻辑

#### 8. 配置管理
- [ ] 连接信息加密存储
- [ ] 自动保存和恢复连接配置
- [ ] 日志目录选择
- [ ] 本地日志文件持久化

#### 9. 高级功能
- [ ] 配置文件解析（YAML/JSON/Properties）
- [ ] Key 扫描引擎
- [ ] 批量操作进度显示
- [ ] 错误重试机制

#### 10. 打包发布
- [ ] 生成 Windows 便携版（免安装）
- [ ] 添加应用图标
- [ ] 版本号管理
- [ ] 安装程序

## 项目结构

```
native/windows/
├── NacosSyncTool.Windows.sln              # 解决方案文件
├── src/
│   └── NacosSyncTool.Windows/             # 主项目
│       ├── App.xaml                        # 应用入口
│       ├── App.xaml.cs                     # 应用逻辑 + 依赖注入
│       ├── MainWindow.xaml                 # 主窗口 UI
│       ├── MainWindow.xaml.cs              # 主窗口逻辑
│       ├── Models/                         # 数据模型
│       │   ├── ApiResult.cs                # API 响应结果
│       │   ├── AppMetadata.cs              # 应用元数据
│       │   ├── NacosConnection.cs          # Nacos 连接配置
│       │   └── NacosNamespace.cs           # Namespace 模型
│       ├── Services/                       # 服务层
│       │   ├── LogService.cs               # 日志服务
│       │   └── NacosApiService.cs          # Nacos API 服务
│       ├── ViewModels/                     # 视图模型
│       │   └── MainViewModel.cs            # 主窗口 ViewModel
│       ├── Views/                          # 视图（对话框等）
│       ├── Converters/                     # 值转换器
│       │   ├── CommonConverters.cs         # 通用转换器
│       │   └── LogLevelToColorConverter.cs # 日志颜色转换器
│       └── Themes/                         # 主题资源
│           ├── Shared.xaml                 # 共享样式
│           ├── Forest.xaml                 # 森林主题（默认）
│           ├── Slate.xaml                  # 深灰主题
│           ├── Warm.xaml                   # 暖色主题
│           └── Nexus.xaml                  # 海洋主题
└── tests/
    └── NacosSyncTool.Windows.Tests/       # 单元测试项目
```

## 开发环境要求

- **Windows 10/11**
- **.NET 10.0 SDK** 或更高版本
- **Visual Studio 2022** 或 **JetBrains Rider**

## 构建和运行

### 使用 Visual Studio

1. 双击打开 `NacosSyncTool.Windows.sln`
2. 按 `F5` 运行调试，或 `Ctrl+Shift+B` 构建

### 使用命令行

```bash
# 进入项目目录
cd native/windows

# 还原依赖
dotnet restore

# 构建项目
dotnet build

# 运行项目
dotnet run --project src/NacosSyncTool.Windows/NacosSyncTool.Windows.csproj
```

### 发布便携版

```bash
# 发布 Windows x64 便携版
dotnet publish src/NacosSyncTool.Windows/NacosSyncTool.Windows.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o publish/win-x64
```

## 主题预览

应用内置了 4 个精心设计的主题：

1. **Forest（森林）** - 默认主题，清新自然的绿色系
2. **Slate（深灰）** - 专业沉稳的灰色系
3. **Warm（暖色）** - 温暖舒适的橙色系
4. **Nexus（海洋）** - 现代清爽的蓝色系

用户可以在应用右上角随时切换主题。

## 使用说明

### 1. 连接 Nacos

#### 源端（只读）
1. 输入源端 Nacos 地址（例如：`http://192.168.1.100:8848`）
2. 输入账号和密码
3. 点击"测试连接"
4. 连接成功后，自动拉取 Namespace 列表
5. 从下拉框选择源 Namespace

#### 目标端
1. 输入目标端 Nacos 地址
2. 输入账号和密码
3. 点击"测试连接"
4. 连接成功后，自动拉取 Namespace 列表
5. 从下拉框选择目标 Namespace，或点击"新建"创建新 Namespace

### 2. 同步配置

（功能待实现）

### 3. 查看日志

- 所有操作都会实时记录在底部日志面板
- 日志使用颜色区分级别：
  - 灰色：普通信息
  - 绿色：成功操作
  - 橙色：警告信息
  - 红色：错误信息
- 点击"清空日志"可清除当前日志

## 代码规范

### 命名约定
- **类名**：PascalCase（例如：`NacosApiService`）
- **方法名**：PascalCase（例如：`TestConnectionAsync`）
- **私有字段**：_camelCase（例如：`_httpClient`）
- **属性**：PascalCase（例如：`SourceAddress`）
- **局部变量**：camelCase（例如：`namespaceId`）

### 异步编程
- 所有 I/O 操作使用异步方法
- 异步方法名以 `Async` 结尾
- 使用 `await` 而不是 `.Result` 或 `.Wait()`

### MVVM 模式
- **Model**：纯数据模型，不包含业务逻辑
- **ViewModel**：UI 状态 + 命令 + 业务逻辑
- **View**：纯 XAML UI，最少的代码后置

## 贡献指南

欢迎贡献代码！请遵循以下步骤：

1. Fork 本仓库
2. 创建功能分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 开启 Pull Request

## 许可证

本项目采用 MIT 许可证。详见 LICENSE 文件。

## 联系方式

- 项目地址：[nacos-sync-tool](https://github.com/your-repo/nacos-sync-tool)
- 问题反馈：[Issues](https://github.com/your-repo/nacos-sync-tool/issues)

---

**注意**：本原生版本与 Electron 版本功能对等，可根据需求选择使用。
