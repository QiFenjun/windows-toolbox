# Windows工具箱

Windows 工具箱是一款离线、模块化的 Windows 桌面工具集。项目使用 WPF、MVVM 和 .NET 8 构建，主程序只负责模块发现、导航、主题与通用外壳，具体工具以独立模块接入。

当前版本：`v1.4.0`

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
- 优先使用注册表 `DisplayIcon` 和 Windows Shell 显示软件真实图标；不可用时才显示统一占位图标
- 图标异步限流加载并按源文件修改时间缓存，不阻塞软件基础信息显示
- 显示安装程序提供的系统报告大小，并支持按需扫描安装目录
- 支持按名称、大小、安装日期和发布者排序
- 支持名称、版本、发布者搜索，以及发布者、来源和系统组件筛选
- 打开软件安装位置、复制软件信息
- 经二次确认后调用软件自身登记的卸载程序
- 不直接删除软件文件、注册表项或所谓“残留”

### 网络流量

- 通过 Windows Kernel Network ETW 按进程统计 TCP/UDP 上传和下载元数据
- 仅在用户启动高级监控时按需提升辅助模式；主程序及其他模块保持普通用户权限
- 支持 IPv4/IPv6 端点、PID + 进程启动时间身份、多进程软件聚合和本次监控累计
- 优先使用 ETW payload 中的真实 PID，并拒绝 PID 0、负 PID 与旧版非法历史记录
- 使用固定专属 ETW 会话、单 Helper 互斥和双向管道优雅停止，异常退出后仅回收本软件孤儿会话
- 应用流量与网络接口实际流量分开显示，避免 VPN/Tunnel 或本地代理重复相加
- 基于系统代理端口、代理进程和外联状态高置信识别本地代理；接口类型结合 PPP、Tunnel、Wintun/WireGuard/TAP 等信息标记 VPN/Tunnel
- 提供最近 60 秒上传/下载曲线与当日仅字节数本地汇总
- 可选关闭主窗口后最小化到系统托盘继续监控；可选登记当前用户的 Windows Run 启动项，默认均关闭
- 不读取数据包正文、URL、Cookie、密码或 HTTPS 内容，也不会上传网络活动记录

### Clipboard+

- 可选启用的纯 Unicode 文本剪贴板历史；默认关闭，暂停状态会保存
- 使用系统剪贴板监听消息，不轮询；支持 Win+Alt+V 快捷键（注册失败会显示提示）
- 支持搜索、复制、删除、置顶、清空、容量（100/300/500/1000）和保留期（1/7/30/90 天或永久）
- 历史文件位于 `%LocalAppData%\\WindowsToolbox\\ClipboardPlus\\history.dat`，使用当前用户 DPAPI 加密，无明文回退
- 只保存纯文本，单条上限 256 KiB；源进程路径优先使用剪贴板 owner window 解析，无法可靠获取时显示“未知来源”
- 支持按进程名或可执行文件路径排除应用；官方剪贴板隐私元数据在系统不可可靠提供时不作猜测

### Text Tools

- 独立的离线文本处理模块，不保存输入、输出或剪贴板内容
- 支持大小写、空白、行处理、查找替换、JSON、URL、Base64、Unicode 转义和统计
- 输入与输出分离，支持复制输出、替换输入、清空和一次性粘贴
- 小文本 180ms 防抖实时处理；超过 1 MiB 自动切换为手动处理，避免界面卡顿

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
2. 下载 `WindowsToolbox-v1.4.0-win-x64.zip`。
3. 解压 ZIP 后双击 `Windows工具箱.exe`。

普通用户无需下载 GitHub 自动生成的 `Source code (zip)` 或 `Source code (tar.gz)`；它们是源码快照，不是可直接运行的软件。

正式 Release 包为 Windows 10/11 64 位系统准备，已经包含所需的 .NET 运行时，无需另外安装 .NET。主程序默认无需管理员权限；只有用户启动网络流量的高级 ETW 监控时，辅助模式才会按需显示 Windows UAC 确认。

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

网络流量模块使用 Microsoft 的 `Microsoft.Diagnostics.Tracing.TraceEvent` 读取 Windows ETW；测试项目使用 MSTest。

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
│  └─ WindowsToolbox.Modules.ClipboardPlus/ # Clipboard+ 效率模块
│     ├─ Models/
│     ├─ Services/
│     ├─ ViewModels/
│     └─ Views/
│  └─ WindowsToolbox.Modules.NetworkTraffic/ # 独立网络流量模块
│     ├─ Models/
│     ├─ Services/
│     ├─ ViewModels/
│     └─ Views/
│  └─ WindowsToolbox.Modules.TextTools/      # Text Tools 文本效率模块
│     ├─ Models/
│     ├─ Services/
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
- `Motion.xaml`：统一动画时长与 Easing
- `ScrollBarStyles.xaml`：全局深浅主题滚动条模板

主题由 `ThemeService` 切换。跟随系统模式只读取当前用户的 Windows 应用主题，不修改系统设置。

设置页的“界面动画”可选择完整、减少或关闭。完整使用全部过渡，减少将时长减半，关闭会跳过主题、侧边栏和窗口内容动画；三档设置均保存到 `%AppData%\\WindowsToolbox\\settings.json`。

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

网络流量“今天”汇总仅保存于：

```text
%LocalAppData%\WindowsToolbox\NetworkTraffic\daily-traffic.json
```

文件仅保存应用标识、应用名、必要路径哈希、日期及上传/下载总字节数；不保存地址、域名、URL、数据包或通信正文。

用户在设置页主动启用“随 Windows 启动后台监控”后，程序才会在当前用户的 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 增加 `WindowsToolbox.NetworkTraffic` 启动项；关闭该选项并保存会删除该项。默认不创建启动项。

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
    --output artifacts/release/v1.4.0/WindowsToolbox-win-x64
```

GitHub 源码仓库不提交 `artifacts`、EXE、ZIP、PDB、`bin` 或 `obj`。可下载的软件仅通过 GitHub Releases 发布。

## 安全与隐私

- 完全离线运行，不收集或上传数据
- 已安装软件列表和目录大小缓存只保存在本机，不会上传
- 主程序不请求管理员权限；网络流量高级 ETW 采集仅在用户明确启动时以命名管道辅助模式按需提升
- 网络流量模块只处理时间、PID、协议、字节数和端点等统计元数据，不抓取数据包正文、不解密 HTTPS、不读取 Cookie、密码或网页内容
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
- 应用管理以传统桌面软件注册表数据为可靠基础；Microsoft Store / MSIX 枚举和 WinGet 精确匹配尚未启用。
- 部分软件没有登记安装位置、大小或可靠卸载命令，此时对应信息显示为“未知”，相关操作会被禁用。
- 应用管理目前以传统桌面软件注册表数据为基础；没有可靠 `DisplayIcon` 或顶层主程序候选的软件会继续使用统一占位图标。MSIX/AppX 专用图标和开始菜单快捷方式匹配尚未启用。
- 网络流量的 ETW 应用归因依赖 Windows 提供的 Kernel Network 事件；透明 WFP/NDIS 重定向若无法可靠归因，会显示“未知”而不会猜测。当前不包含驱动、WFP Callout、VPN/代理配置或网络拦截功能。
- 网络接口实时统计使用 Windows IP Helper/网络接口信息；应用流量与接口流量是独立概念，不能相加。真实 VPN 或本地代理环境仅在系统已存在时可验证，项目不会安装第三方 VPN/代理。
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
