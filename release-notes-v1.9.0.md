# Windows 工具箱 v1.9.0

v1.9.0 新增一个静态注册的 **小工具 / Utilities** 模块，包含 **二维码工具 / QR Tools** 与 **颜色工具 / Color Tools**。现有顶层模块 ID 保持不变，两个内部工具使用固定 ID `qr`、`color`。

QR Tools 可离线生成和识别 QR Code，支持中文、Emoji、PNG 复制/导出，以及本地图片、拖放和用户主动读取剪贴板图片。识别出的 URL 只显示，不会自动打开。Color Tools 提供 HEX、RGB、HSL、Alpha 转换、透明棋盘预览、格式复制、会话内最近颜色和屏幕取色。

屏幕取色只读取光标当前的屏幕像素。它使用 Win32 物理屏幕坐标与短生命周期 Overlay；确认后只更新工具界面，不修改剪贴板、不保存截图或颜色历史。Utilities 不联网、不自动保存输入或历史；QR PNG 只在用户主动导出时写入所选路径。

QR 编解码依赖 ZXing.Net 0.16.11（Apache-2.0）。完整许可证文本与第三方组件说明随发布 ZIP 一并提供。

## 验证

- Release build：0 warnings / 0 errors
- Release tests：468/468 passed；普通自动化测试使用 Fake，不读取真实屏幕或实际阻止睡眠
- QR ASCII、中文和 Emoji encode/decode 往返通过
- 本机真实 screen sampler 对自建纯色窗口采样 500 次，GDI 对象数 116 → 116
- Light/Dark UI smoke、Restart Manager 临时文件集成、Keep Awake 立即释放、20,000 文件压力检查通过

**Manual UI Verification Pending：** 尚未人工点击 WPF 主窗口、二维码导出/手机扫描、屏幕取色确认与取消。多显示器、第二显示器负坐标及 100%/125%/150% 混合 DPI 也未在对应硬件环境验证；离屏 UI smoke 和单窗口采样集成不代表这些场景已人工验收。细节见 [`docs/v1.9.0-validation.md`](docs/v1.9.0-validation.md)。

下载 `WindowsToolbox-v1.9.0-win-x64.zip`，解压后运行 `Windows工具箱.exe`；SHA-256 见随包提供的 `SHA256SUMS.txt`。
