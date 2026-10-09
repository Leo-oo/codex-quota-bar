# Mac 1.0.2 publication validation

2026-10-09，Windows发布会话读取真实Mac1.0.2源码ZIP及Universal DMG；没有运行、挂载或操作Mac应用，没有读取实际Codex或认证状态。

- 源码ZIP：603,912 bytes，SHA256 `1c6b499841e8c574233382d827010faab34d3463cd241be56f6d84310f7cb7d9`；124文件安全提取，路径/大小写/加密/符号链接检查及CRC通过，未发现.git、真实runtime或凭据文件。
- DMG：1,083,640 bytes，SHA256 `e4456fa2e51bc9e4a7df072e0f91a9bfd372632d7c0ef08e85722b6c8bc1943c`，匹配原交付。
- 从UDIF数据解压到APFS映像后，静态定位完整FAT Mach-O候选，架构为x86_64与arm64，两LC_BUILD_VERSION最低macOS13.0。版本元数据1.0.2/build102，与签名特殊槽绑定。
- 两CodeDirectory均为ad-hoc，245个代码页哈希匹配；无TeamID，CMS负载为空。未完整解析APFS目录，因此不独立证明整个DMG目录布局，也不证明Apple公证、Gatekeeper或实机运行。
- AppIcon.icns八个PNG帧RGBA与本项目原创黑色共享PNG一致。EXIF仅含色彩空间、尺寸及结构偏移，未发现个人描述/位置/设备标识或私人路径。
- 原交付历史文本记录344（21+323）及384通过；无结构化原生报告。384是application-local NSEvent合成队列。本Windows会话没有重新执行Mac检查，不把这些历史记载冒充本轮运行结果。

Mac生产Swift和五个shell脚本保持交付原字节。当前README与安装说明按正式MIT、真实构建/测试前提及安全流程更新；第三方完整NOTICE及上游MIT保留。没有把任何旧Windows源码纳入macos目录。
