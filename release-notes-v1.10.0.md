# Windows 工具箱 v1.10.0

v1.10.0 在现有静态 **小工具 / Utilities** 容器中增加 **时间工具 / Time Tools** 与 **随机工具 / Random Tools**。Utilities 仍是唯一的顶层模块，内部仅通过固定 ID 导航；已有 QR Tools 与 Color Tools 的业务逻辑保持不变。

Time Tools 离线处理 Unix 秒/毫秒、DateTimeOffset、ISO 8601、Windows 本机时区和精确时间差。DST 跳时的无效本地时间会被拒绝；DST 回拨的重复时间会列出两个 Offset，要求用户明确选择。当前时间只在 Time Tools 页面可见时每秒刷新。

Random Tools 可生成 UUID v4、批量 UUID、安全随机字符串和包含上下界的随机整数。字符选择使用 `RandomNumberGenerator.GetInt32`，整数范围使用密码学随机字节和拒绝采样；字符串最多 4,096 字符、最多 1,000 项，合并输出上限为 1 MiB。生成结果只在当前会话中显示，支持单项或全部复制，不写入历史、文件、日志或 telemetry。

两个新工具均完全离线，不修改 Windows 系统时间、时区、电源计划或注册表。随机字符串是通用随机文本工具，不声称提供密码复杂度或强度保证。

## 验证

- Release build：0 warnings / 0 errors
- Release tests：497/497 passed；时间与随机字符串/整数单元测试使用 Fake
- 独立生产 CSPRNG smoke 对 UUID v4、随机字符串和整数各生成并验证 100 项，不输出生成值；该检查不是统计随机性或密码安全证明
- Light/Dark 离屏 WPF smoke 覆盖现有模块、四个 Utilities 内页及固定内部导航
- 既有 QR、Color、Restart Manager、Keep Awake、压力集成将按发布验证记录执行

**Manual UI Verification Pending：** 尚未人工点击主窗口验证 Time/Random 参数交互、真实 Clipboard、窄窗口布局与键盘辅助。第二显示器、混合 DPI、真实手机扫码等既有人工硬件项目仍为 Pending；自动布局 smoke 不代表这些场景已人工验收。自动化验证与剩余人工项目见 `docs/v1.10.0-validation.md`。

下载 `WindowsToolbox-v1.10.0-win-x64.zip`，解压后运行 `Windows工具箱.exe`；SHA-256 见随包提供的 `SHA256SUMS.txt`。
