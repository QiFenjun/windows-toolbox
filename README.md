# Windows工具箱

Windows 工具箱是一款离线、模块化的 Windows 桌面工具集。项目使用 WPF、MVVM 和 .NET 8 构建，主程序只负责模块发现、导航、主题与通用外壳，具体工具以独立模块接入。

当前版本：`v1.1.0`

## 当前模块

### 定时关机

- 自定义日期、小时和分钟
- 30 分钟、1 小时、2 小时、今晚 23:00 快捷选择
- 显示预计关机时间和实时剩余时间
- 一键取消 Windows 关机计划
- 关闭应用后不取消已经创建的系统计划
- 旧版 WinForms 计划状态自动迁移

### 应用管理

- 从明确的 32 位和 64 位注册表视图读取传统桌面软件
- 查看软件名称、版本、发布者、安装日期、架构和安装位置
- 显示安装程序提供的系统报告大小，并支持按需扫描安装目录
- 支持按名称、大小、安装日期和发布者排序
- 支持名称、版本、发布者搜索，以及发布者、来源和系统组件筛选
- 打开软件安装位置、复制软件信息
- 经二次确认后调用软件自身登记的卸载程序
- 不直接删除软件文件、注册表项或所谓“残留”

## 界面结构

- 左侧：应用标识、首页、动态模块导航、设置、关于、侧边栏折叠
- 顶部：页面标题、模块搜索、主题快捷切换、窗口控制
- 主区：首页工具卡片、最近使用、模块页面
- 主题：浅色、深色、跟随系统

公共颜色、字体、按钮、卡片和控件样式分别位于 `Themes` 目录中的 ResourceDictionary。模块页面不重复定义这些基础视觉属性。

## 项目截图

项目截图将在后续版本补充，计划存放在 `.github/images/` 目录。当前预留：

- `.github/images/home.png`
- `.github/images/shutdown.png`
- `.github/images/installed-apps.png`

## 安装与运行

### 使用发布包

1. 前往 [GitHub Releases](https://github.com/QiFenjun/windows-toolbox/releases)。
2. 下载 `WindowsToolbox-v1.1.0-win-x64.zip`。
3. 解压 ZIP 后双击 `Windows工具箱.exe`。

普通用户无需下载 GitHub 自动生成的 `Source code (zip)` 或 `Source code (tar.gz)`；它们是源码快照，不是可直接运行的软件。

正式 Release 包为 Windows 10/11 64 位系统准备，已经包含所需的 .NET 运行时，无需另外安装 .NET。软件无需管理员权限，也不会创建网络连接。

### 从源码启动

```powershell
Set-Location .\outputs\Windows工具箱
dotnet restore WindowsToolbox.sln
dotnet run --project src/WindowsToolbox.App/WindowsToolbox.App.csproj
```

## 开发环境

- Windows 10/11
- .NET 8 SDK
- Visual Studio 2022 17.8 或更高版本（可选）
- WPF
- C# 12

生产项目没有第三方运行时依赖；测试项目仅使用 MSTest。

## 项目目录

```text
windows-toolbox/
├─ outputs/Windows工具箱/
│  ├─ src/
│  ├─ WindowsToolbox.App/                 # WPF 外壳、首页、设置、主题
│  │  ├─ Assets/
│  │  ├─ Converters/
│  │  ├─ Services/
│  │  ├─ Themes/
│  │  ├─ ViewModels/
│  │  └─ Views/
│  ├─ WindowsToolbox.Core/                # 模块、导航、设置、命令等契约
│  │  ├─ Commands/
│  │  ├─ Interfaces/
│  │  ├─ Models/
│  │  ├─ Services/
│  │  └─ Utilities/
│  ├─ WindowsToolbox.Modules.Shutdown/    # 独立定时关机模块
│     ├─ Models/
│     ├─ Services/
│     ├─ ViewModels/
│     └─ Views/
│  └─ WindowsToolbox.Modules.InstalledApps/ # 独立应用管理模块
│     ├─ Models/
│     ├─ Services/
│     ├─ Utilities/
│     ├─ ViewModels/
│     └─ Views/
│  ├─ tests/WindowsToolbox.Tests/
│  ├─ legacy/WinForms-v1/                  # 原版源码备份；编译产物不入库
│  ├─ scripts/
│  └─ WindowsToolbox.sln
├─ .gitignore
└─ README.md
```

## 模块系统如何工作

1. 每个模块实现 `IToolModule`，提供 ID、名称、分类、说明、图标键、关键词、排序和可用性。
2. `ModuleRegistry` 负责注册、排序、查找与搜索。
3. `NavigationService` 使用页面 ID 导航，并缓存 ViewModel，避免重复创建页面。
4. 模块通过自己的 `ModuleResources.xaml` 提供 ViewModel 到 View 的 DataTemplate。
5. 应用启动时加载已注册模块的资源字典，主窗口根据注册表自动生成侧边栏、首页卡片和搜索结果。

因此新增模块不需要修改 `MainWindow.xaml` 或 `MainWindow.xaml.cs`。

## 添加新模块

建立一个新的 WPF 类库并引用 `WindowsToolbox.Core`：

```csharp
public sealed class ClipboardModule : IToolModule
{
    public string Id => "clipboard";
    public string DisplayName => "剪贴板工具";
    public string Description => "查看和处理剪贴板文本";
    public string Category => "效率工具";
    public string IconKey => "Toolbox";
    public int SortOrder => 200;
    public bool IsAvailable => OperatingSystem.IsWindows();
    public IReadOnlyList<string> Keywords => ["剪贴板", "文本"];
    public string ResourceDictionaryPath =>
        "/WindowsToolbox.Modules.Clipboard;component/ModuleResources.xaml";

    public object CreateViewModel() => new ClipboardViewModel();
}
```

模块的 `ModuleResources.xaml`：

```xml
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:vm="clr-namespace:WindowsToolbox.Modules.Clipboard.ViewModels"
    xmlns:views="clr-namespace:WindowsToolbox.Modules.Clipboard.Views">
    <DataTemplate DataType="{x:Type vm:ClipboardViewModel}">
        <views:ClipboardView/>
    </DataTemplate>
</ResourceDictionary>
```

最后在 `App.xaml.cs` 的组合根中注册一次：

```csharp
moduleRegistry.Register(new ClipboardModule());
```

侧边栏、首页卡片、模块数量、搜索和导航会自动更新。

## 图标和资源

- 当前图标使用 Windows 自带的 Segoe Fluent Icons，避免额外依赖。
- 模块通过 `IconKey` 使用统一图标映射。
- 新图标键在 `IconKeyToGlyphConverter` 中集中添加。
- 位图、应用图标等文件放入 `WindowsToolbox.App/Assets`，不要散落在页面目录。

## 主题

- `Colors.xaml`：稳定的品牌色和状态色
- `Colors.Light.xaml`：浅色语义资源
- `Colors.Dark.xaml`：深色语义资源
- `Typography.xaml`：字体层级
- `ButtonStyles.xaml`：按钮和导航样式
- `CardStyles.xaml`：卡片和提示条样式
- `ControlStyles.xaml`：输入控件样式

主题由 `ThemeService` 切换。跟随系统模式只读取当前用户的 Windows 应用主题，不修改系统设置。

## 用户设置

设置保存在：

```text
%AppData%\WindowsToolbox\settings.json
```

关机计划显示状态保存在：

```text
%LocalAppData%\WindowsToolbox\shutdown-plan.json
```

这些状态文件不改变 Windows 的关机机制。实际计划仍由系统自带的 `shutdown.exe` 管理。

## 编译与测试

```powershell
Set-Location .\outputs\Windows工具箱
dotnet restore WindowsToolbox.sln
dotnet build WindowsToolbox.sln --configuration Release --no-restore
dotnet test WindowsToolbox.sln --configuration Release --no-build
```

也可以运行：

```powershell
.\outputs\Windows工具箱\scripts\build.ps1
```

## 打包

生成完全自包含的 Windows x64 单文件版本：

```powershell
dotnet publish outputs/Windows工具箱/src/WindowsToolbox.App/WindowsToolbox.App.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=None `
  -p:DebugSymbols=false `
  --output artifacts/release/v1.1.0/WindowsToolbox-win-x64
```

GitHub 源码仓库不提交 `artifacts`、EXE、ZIP、PDB、`bin` 或 `obj`。可下载的软件仅通过 GitHub Releases 发布。

## 安全与隐私

- 完全离线运行，不收集或上传数据
- 已安装软件列表和目录大小缓存只保存在本机，不会上传
- 不请求管理员权限
- 不修改 Windows 安全或关键系统设置
- `shutdown.exe` 参数由程序内部生成，不接收任意命令文本
- 应用管理只调用软件自身登记的卸载程序，不直接删除目录或注册表残留
- 默认不使用静默卸载命令，不提供批量卸载或“一键清理”
- 不伪造或绕过代码签名、安全提示
- 未签名版本可能触发 Windows Defender SmartScreen 的“未知发布者”提示

应用管理中的“系统报告大小”来自安装程序写入的 `EstimatedSize`，可能与实际磁盘占用不同。“目录扫描”只统计已知安装位置，不包括共享运行库、用户数据和系统缓存。

## 已知问题

- Windows 没有为普通桌面应用提供可靠的“查询全部待执行关机计划”接口，因此界面只跟踪本应用创建并保存的计划；取消操作仍调用系统的 `shutdown /a`。
- 跟随系统主题在应用启动时读取；Windows 运行期间切换系统主题后，需要重新打开应用或在设置页重新选择。
- 应用管理 v1.1.0 以传统桌面软件注册表数据为可靠基础；Microsoft Store / MSIX 枚举和 WinGet 精确匹配尚未启用。
- 部分软件没有登记安装位置、大小或可靠卸载命令，此时对应信息显示为“未知”，相关操作会被禁用。
- 应用图标目前使用统一的 Fluent 默认图标，避免启动时一次性加载大量高分辨率资源。
- 未购买商业代码签名证书，发布的 EXE 为未签名程序。

## 后续规划

- 增加 Microsoft Store / MSIX 官方 API 支持
- 在具有可靠唯一 ID 时补充 WinGet 信息
- 增加独立的小型工具模块
- 增加多语言资源切换
- 增加模块级诊断日志（保持本地、可关闭）
- 在有代码签名证书后签名正式发布包

## 许可证

当前项目尚未指定开源许可证。在许可证文件正式加入仓库前，作者保留全部权利；公开源码不代表自动授予复制、修改、分发或商业使用许可。
