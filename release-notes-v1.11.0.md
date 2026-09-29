# Windows 工具箱 v1.11.0

v1.11.0 在现有静态 **小工具 / Utilities** 容器中增加 **单位转换 / Unit Converter** 与 **开发者工具 / Developer Tools**。Utilities 仍是唯一的顶层模块，内部仅通过固定 ID 导航；已有 QR、Color、Time、Random 工具的业务逻辑保持不变。没有新增 Sidebar 模块、插件系统或动态加载框架。

## 单位转换 / Unit Converter

- 支持长度、质量、温度、面积、体积、速度、压力、能量、功率、角度和数据大小 11 个类别
- 数据大小严格区分 SI（KB = 1000 B）与 IEC（KiB = 1024 B），MB ≠ MiB
- 温度使用独立的仿射公式（°C ↔ °F ↔ K），拒绝低于绝对零度的输入，不做 `value × factor` 误算
- 数值以 `decimal` 计算，结果智能格式化：去除无意义尾零、最多约 12 位有效数字、极端值使用 `1E-9` 风格科学计数法
- 支持交换单位、复制数值、复制数值与单位；输入支持整数、小数、负数与科学计数法
- 完全离线，输入与结果仅保留在当前会话

## 开发者工具 / Developer Tools

- 进制转换：二/八/十/十六进制任意精度整数（`BigInteger`），最多 4096 字符，前缀 `0b` / `0o` / `0x` 必须与所选进制一致，HEX 默认大写，可选显示前缀
- 文本哈希：UTF-8 文本 MD5、SHA-1、SHA-256、SHA-512（标准向量验证），默认 SHA-256；显式点击计算，输入变更标记结果待重新计算
- UUID 检查：D/N/花括号输入验证与标准化，输出 Canonical D/N、Version 与 Variant（NCS / RFC 4122·9562 / Microsoft / Future Reserved），识别 Nil UUID 与 Max UUID；不生成 UUID（生成请用随机工具）
- 文件 Hash 由文件工具 / File Tools 负责；JSON / Base64 / URL / Unicode 由文本工具负责，本版本不重复实现

## Privacy

- Unit Converter 与 Developer Tools 均完全离线
- Hash 输入、UUID 和转换记录默认不持久保存
- 文本 Hash 内容不会写入日志

## Manual UI Verification Pending

尚未人工点击主窗口验证单位转换与开发者工具的鼠标/键盘交互、真实剪贴板复制、窄窗口视觉、125%/150% 混合 DPI 与第二显示器场景。离屏 Light/Dark 自动 smoke 不代表这些场景已人工验收。自动化验证见 `docs/v1.11.0-validation.md`。

下载 `WindowsToolbox-v1.11.0-win-x64.zip`，解压后运行 `Windows工具箱.exe`；SHA-256 见随包提供的 `SHA256SUMS.txt`。
