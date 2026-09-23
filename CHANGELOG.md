# 更新日志

本项目使用语义化版本号记录正式发布。

## [1.9.0] - 2026-09-23

### Added

- 新增静态 Utilities 顶层模块（ID `utilities`），内含固定导航项 QR Tools（`qr`）与 Color Tools（`color`）
- QR Tools 支持离线文本/URL/中文/Emoji 生成，PNG 复制与导出，以及本地单张图片、拖放和用户主动读取剪贴板图片识别
- Color Tools 支持 HEX、RGB、HSL、Alpha 相互转换，透明棋盘预览、格式复制、会话内最近颜色和屏幕取色
- 屏幕取色使用 Per-Monitor V2 物理屏幕坐标、短生命周期虚拟桌面 Overlay、GetDC/GetPixel，并在取消、失焦、窗口隐藏和应用退出时清理
- 发布包附带 ZXing.Net 0.16.11 的 Apache-2.0 许可证文本与第三方组件说明

### Privacy and safety

- 两个工具完全离线；应用不持久化二维码输入、识别图片或颜色历史（用户主动导出 PNG 除外），识别出的 URL 不自动打开
- 屏幕取色只读当前像素，不保存截图、不读取窗口内容或标题；不注入、不提权、不写入剪贴板

### Validation

- Release build 0 warnings / 0 errors；全量自动化测试 468/468 通过，所有外部 API、剪贴板和 screen picker 单元测试使用 Fake
- QR ASCII/中文/Emoji encode → bitmap → decode 往返通过；Light/Dark UI smoke 通过
- 真实 screen sampler 对自建纯色窗口采样 500 次，颜色正确且 GDI 对象数保持 116 → 116
- 本机真实 Restart Manager、Keep Awake 即时释放和 20,000 文件压力集成验证通过；具体显示器 DPI、人工 UI 和硬件场景仍为 Pending

## [1.8.0] - 2026-09-23

### Added

- 同时新增占用检测 / Lock Inspector 与保持唤醒 / Keep Awake，使用现有静态模块、双语 Metadata、首页及导航
- Lock Inspector 使用 Windows Restart Manager 诊断单/多文件、文件夹与本地驱动器的 best-effort 占用，提供批处理、进度、取消、上限和跳过提示
- 显示应用/服务及 PID + 启动时间身份，支持复制信息、打开程序位置和任务管理器；长路径失败单独提示，不丢弃其他文件的结果
- Keep Awake 支持系统或系统+显示器模式，15/30 分钟、1/2/4 小时及手动停止，单调倒计时和托盘后台保持

### Safety

- Lock Inspector 只诊断，不结束进程、不关闭远程句柄、不提权、不执行弹出；不保存扫描历史、路径日志或文件内容
- Keep Awake 仅在专用稳定线程调用 SetThreadExecutionState，不修改电源计划/注册表、不模拟输入；停止、到期及真正退出时释放并结束工作线程
- 仅保存模式/时长，重启或随 Windows 启动时保持 Inactive，必须由用户重新开启

### Validation

- Release 编译 0 警告 / 0 错误；399/399 普通测试通过（原有 315，新增 84）
- 本机真实 RM 临时锁文件、解除锁、多文件/文件夹、取消/异常会话清理验证通过；396 字符路径被本机 RM 明确拒绝（错误 29），作为已知能力边界呈现
- 两种真实 execution-state 模式均短暂启用并立即释放；普通测试始终使用 Fake
- 独立 20,000 空文件测试验证 79 批、取消、内存、Junction 跳过、拒绝访问目录和夹具清理
- 十个模块 Light/Dark 资源/布局自动化 smoke 通过；人工 UI、USB、多显示器/多 DPI、电池/锁屏/睡眠硬件验收仍为 Pending


## [1.7.0] - 2026-09-22

### Added

- 新增窗口工具 / Window Tools 模块，支持查看普通顶层窗口及窗口信息
- 支持窗口置顶/取消置顶、居中、六种常见位置布局、尺寸预设与自定义尺寸
- 支持移动到其他显示器、窗口标题/进程/PID 搜索与前台窗口捕获

### Security

- 过滤 Desktop、WorkerW、任务栏等关键 Shell 窗口，不读取窗口内容或输入数据
- 不自动提升权限、不绕过 UIPI、不使用 Hook、注入或输入模拟，也不修改 Windows Snap 配置

### Validation

- Release 编译 0 警告 / 0 错误
- 全量测试 315/315 通过

## [1.6.0] - 2026-09-22

### Added

- 新增快捷启动 / Quick Launch 模块，支持固定常用应用、文件夹、文件和 `http` / `https` 网页
- 支持搜索名称、目标和分组，分组、固定、最近使用、上移和下移
- 支持从资源管理器拖入 `.exe`、文件夹和普通文件，并使用 Windows Shell 图标与内存缓存
- 支持 `Win+Alt+Q` 快速显示工具箱、导航到 Quick Launch 并聚焦搜索框
- Quick Launch 配置使用 `DataVersion = 1`、原子写入和损坏文件备份恢复

### Improved

- 统一现有模块的中文与英文显示名称
- 系统工具和效率工具分类增加中英文名称
- 优化展开与折叠侧边栏、首页卡片、搜索结果和工具提示中的双语展示

### Safety

- Quick Launch 仅调用 Windows Shell，不使用 `cmd.exe` 或 PowerShell，不扫描全盘，不联网
- 网页目标仅允许 `http` / `https`；移除快捷项不会删除真实文件、目录或应用

### Validation

- Release 编译 0 警告 / 0 错误
- 全量测试 278/278 通过

## [1.5.0] - 2026-09-22

### Added

- 新增 File Tools 独立效率模块：批量重命名、文件 Hash 校验、路径转换和文件信息
- 批量重命名支持前缀、后缀、查找替换、编号、大小写转换和扩展名变更
- Rename Preview 在执行前检查非法字符、保留设备名、目标冲突、缺失文件、路径过长和循环重命名
- 使用同目录临时文件完成交换/循环重命名，失败时尽力回滚，并支持本次运行内撤销
- 支持 SHA-256、SHA-512、MD5 兼容性校验、流式读取、进度、取消、Expected Hash 和 SHA256SUMS
- 支持 Windows 路径多种格式、PowerShell 单引号转义、File URI、文件属性和可取消文件夹大小扫描
- 支持 Explorer 文件/文件夹拖放、路径去重、Reparse Point 跳过和虚拟化列表

### Safety

- File Tools 完全离线运行，不上传文件或 Hash，不请求管理员权限，不自动覆盖文件
- 所有真实文件系统测试均在隔离临时目录执行

### Validation

- Release 编译 0 警告 / 0 错误
- 全量测试 246/246 通过

## [1.3.0] - 2026-09-22

### Added

- 新增 Clipboard+ 效率工具模块：纯文本剪贴板历史、搜索、快速复制、置顶、删除与清空
- 使用 `AddClipboardFormatListener` 监听剪贴板更新，支持 Win+Alt+V 全局快捷键
- 支持容量与保留期设置、进程名/路径排除、暂停与恢复
- 历史数据使用当前用户 DPAPI 加密并以原子方式保存到 LocalAppData
- 增加 32 项 Clipboard+ 单元测试；所有测试均使用内存假适配器，不修改真实剪贴板

### Privacy

- 仅处理 Unicode 纯文本，拒绝图片、文件、HTML/RTF 等格式；单条文本上限 256 KiB
- 不上传、不联网、不记录剪贴板正文日志；无法可靠识别来源时显示“未知来源”

## [1.2.4] - 2026-09-04

### Improved

- 优化侧边栏展开与收起动画性能，改为最终布局一次确定、内容使用 RenderTransform 过渡
- 优化侧边栏文字和导航图标的出现与隐藏节奏，减少快速连续切换时的视觉跳变
- 优化“最近使用”区域的延迟渐入、渐出与命中测试状态
- 优化窗口最大化与还原时的视觉连续性，等待 Render 帧后仅执行轻量透明度过渡

### Fixed

- 修复侧边栏动画过程中“最近使用”区域突兀出现或收起残留的问题
- 减少侧边栏动画触发主内容重复 Measure/Arrange 导致的轻微卡顿
- 保留深色主题滚动条 Track 状态绑定修复

### Validation

- Release 编译 0 警告 / 0 错误
- Sidebar、Recent、WindowState 动画回归测试全部通过

## [1.2.3] - 2026-09-04

### Improved

- 新增统一 Motion 资源，规范 120/180/240ms 动画时长与 Easing
- 新增浅色与深色主题平滑切换遮罩，连续切换时安全替代上一动画
- 新增侧边栏 232px ↔ 72px GridLength 展开/收起动画，文字使用淡入淡出
- 优化窗口最大化/还原后的内容视觉过渡，不改变 Windows 原生 DWM、Snap 和 DPI 行为
- 启用 UseLayoutRounding、SnapsToDevicePixels 与 Display 文本格式化，移除全局 ClearType 强制和所有 ScaleTransform 动画
- 新增完整、减少、关闭三档界面动画设置，并保存到用户 AppData
- 统一深浅主题 ScrollBar、ToolTip、ContextMenu、CalendarDayButton 与键盘 Focus Ring

### Fixed

- 修复深色模式下内部 ScrollViewer、ListView 和详情页滚动条可能回落到系统浅色样式的问题
- 修复部分公共控件在主题切换时出现默认浅色 Hover、Focus 或 Popup 的问题
- 修复 Motion 触发器资源顺序与可冻结性问题，避免启动时 XamlParseException

### Validation

- Release 编译 0 警告 / 0 错误
- 原有 85 项测试与新增 8 项 UI 资源、Motion 和设置回归测试全部通过（93/93）
- 本地 Release 程序启动冒烟通过，未再出现 XamlParseException 或 .NET Runtime 崩溃

## [1.2.2] - 2026-09-04

### Fixed

- 修复 Kernel Network ETW 事件误用事件头进程 ID、导致流量归入 `PID -1` 的问题；现在优先使用 payload 中的真实 PID
- 拒绝 PID 0、负 PID 与旧版本产生的非法历史记录，并为历史记录增加明确的身份状态
- 修复高级监控异常退出后遗留 System Logger 会话、最终触发 `ETW-800705AA` 的问题
- 使用固定专属 ETW 会话、全局 Helper 互斥和双向命名管道优雅停止，超时后才允许强制终止
- 修复停止时排空大量实时事件造成的超时竞态、管道释放异常和主窗口闪退
- 修复浏览器经本地代理联网时可能误显示为直连的问题，并避免普通 localhost 服务被误判为代理
- 修复高级监控启动后状态标题未同步刷新的问题

### Validation

- 实机清理 3 个仅属于 Windows 工具箱的旧孤儿会话后，`ETW-800705AA` 消失
- 实机验证 Chrome、Codex 与 verge-mihomo 独立归因，以及 `127.0.0.1:7897` 本地代理路径
- 连续 20 次 ETW 启动/停止均未留下会话

## [1.2.1] - 2026-09-04

### Improved

- 应用管理优先显示已安装软件在 Windows 卸载注册表中登记的真实图标
- 支持解析带引号、环境变量、正负资源索引及路径内逗号的 `DisplayIcon` 值
- 使用 Windows Shell 提取 EXE、DLL、ICO 和快捷方式资源，并在图标不可用时回退统一占位图标
- 图标在后台限流加载，按源文件修改时间进行内存缓存；列表保留虚拟化，避免图标加载阻塞基础数据展示
- 安装目录后备只检查顶层候选并排除卸载器、安装器、更新器和 `msiexec`

## [1.2.0] - 2026-09-04

### Added

- 新增网络流量监控独立模块
- 支持按应用实时查看 TCP/UDP 上传和下载速度，以及本次累计流量
- 支持 IPv4/IPv6 端点、PID + 进程启动时间身份和多进程软件聚合
- 支持网络接口实际流量统计，以及 VPN/Tunnel/Loopback 接口分类
- 支持使用本地 TCP/UDP 监听端口识别本地代理路径
- 支持直连、VPN/Tunnel、本地代理、Loopback 和未知路径标记
- 支持最近 60 秒实时曲线与当日仅字节数本地汇总

### Privacy

- 网络流量分析完全在本机进行
- 不读取或保存通信正文、URL、Cookie、密码或 HTTPS 内容
- 不上传网络活动记录

## [1.1.1] - 2026-07-25

### Fixed

- 修复深色主题下拉框悬停时出现整块系统浅蓝色背景的问题
- 统一定时关机、应用管理和设置页下拉框的悬停与选中状态
- 移除下拉选项中的系统虚线焦点框

## [1.1.0] - 2026-07-25

### Added

- 新增“应用管理”独立模块
- 支持从 Windows 32 位、64 位和当前用户卸载注册表视图读取传统桌面软件
- 支持查看软件名称、版本、发布者、安装日期、架构和安装位置
- 支持系统报告大小与按需目录扫描
- 支持目录扫描取消、重解析点跳过和本地大小缓存
- 支持按名称、大小、安装日期和发布者排序
- 支持名称、版本、发布者搜索，以及发布者、来源和系统组件筛选
- 支持打开安装位置和复制软件信息
- 支持经二次确认后调用软件自身登记的卸载程序
- 新增可复用的 GitHub Release 发布脚本

### Changed

- 程序版本更新为 1.1.0
- 关于页面改为读取程序集版本
- 更新 README、模块说明和隐私说明

## [1.0.0] - 2026-07-23

### Added

- 首次公开发布模块化 Windows 工具箱
- 提供定时关机模块
- 提供浅色、深色和跟随系统主题
## [1.4.0] - 2026-09-22

### Added

- 新增 Text Tools 独立效率模块：大小写、空白、行处理、查找替换、JSON、URL、Base64、Unicode 转义与文本统计
- 增加模块内统一操作注册表、三栏输入/输出界面、搜索、复制、替换输入、清空和粘贴操作
- 小文本支持 180ms 防抖实时处理，超过 1 MiB 时改为手动处理；处理错误不会覆盖上一次输出
- 增加 68 项 Text Tools 回归测试，覆盖 Unicode、JSON 大整数、无效编码和行尾策略

### Changed

- 应用版本更新为 1.4.0，主程序组合根注册 Text Tools，侧边栏、首页和搜索自动读取模块元数据
