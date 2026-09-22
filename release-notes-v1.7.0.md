## Windows 工具箱 v1.7.0

### Added

- 新增窗口工具 / Window Tools：查看、搜索和安全管理普通顶层窗口
- 支持置顶、取消置顶、居中、六种位置布局、尺寸预设/自定义尺寸与多显示器移动
- 支持查看窗口 HWND、进程、外框/客户区物理像素、显示器、工作区和 DPI

### Security

- 不读取窗口内容、不截图、不注入、不 Hook、不模拟输入、不自动提权
- 过滤关键 Windows Shell 窗口；高权限窗口受 UIPI 限制时显示友好错误

### Validation

- `dotnet build -c Release --no-restore`: 0 warnings / 0 errors
- `dotnet test -c Release --no-build`: 315/315 passed
