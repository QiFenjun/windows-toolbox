## [1.5.0]

### Added

- 新增 File Tools 文件处理模块，统一提供批量重命名、文件校验、路径工具和文件信息四个子工具
- 批量重命名支持前缀、后缀、查找替换、编号、大小写转换和扩展名变更，并在执行前展示 Preview
- 增加 Windows 文件名非法字符、保留设备名、目标冲突、缺失文件、路径过长和重命名循环检查
- 使用同目录临时名称安全处理交换/循环重命名，失败时尽力回滚，并支持当前运行期间撤销上次批次
- 支持 SHA-256、SHA-512、MD5 兼容性校验；采用流式读取、有限并发策略、进度、取消和 Expected Hash 比较
- 支持生成 `SHA256SUMS.txt`，不会在未确认时覆盖已有文件
- 支持完整路径、文件名、Stem、扩展名、父目录、正斜杠、带引号路径、PowerShell LiteralPath 和 File URI
- 支持文件属性、版本信息和可取消的文件夹大小统计，默认不跟随 Reparse Point
- 支持 Explorer 文件/文件夹拖放、路径去重和虚拟化文件列表

### Safety

- File Tools 保持普通用户权限，不请求管理员权限，不调用网络服务，不上传文件或 Hash
- Rename 始终先 Preview，存在冲突或非法名称时禁用执行
- 测试只使用隔离临时目录，不操作用户 Documents、Desktop、Downloads 或项目源文件

### Validation

- Release build：0 warnings / 0 errors
- Automated tests：246 passed
