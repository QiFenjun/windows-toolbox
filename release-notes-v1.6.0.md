## Windows 工具箱 v1.6.0

### Added

- 新增快捷启动 / Quick Launch 模块
- 支持应用、文件夹、文件和 `http` / `https` 网页快捷项
- 支持搜索、分组、固定、最近使用、拖放和 Win+Alt+Q 全局快捷键
- 使用 Windows Shell 图标和目标存在性检查，配置损坏时自动备份并恢复为空列表

### Improved

- 所有已安装模块、系统工具和效率工具分类统一增加中文与英文显示名称
- 侧边栏、首页卡片、搜索结果和折叠状态工具提示均使用双语层级显示

### Validation

- `dotnet build -c Release --no-restore`: 0 warnings / 0 errors
- `dotnet test -c Release --no-build`: 278/278 passed
