# CodexUsageMini for Windows 0.11.10

本版修复已确认公共事件缺少 `occurredOn` 时跨天历史丢失的问题。保留最近一条有效确认事件：发生日期优先，缺失时回退有时区确认时间或可靠确认日期，显示“发生日期／确认时间／确认日期”依据；历史按北京时间 UTC+8 显示。两种事件分别标为“上次额度重置”和“上次发放重置卡”。确认时间不是实际到账时间，公共事件不是个人额度证明。

旧 policy3 缓存升级时保留有效历史、跨天活动、已读版本及未读状态，取消 ETag 强制重新获取来源；无效历史清除。旧 policy1/2及未知缓存保留既有清理边界。历史变化不变为活动未读。消息投影、窗口跟随、入口/菜单、额度数据、位置偏好及用户选定 2 号图标保持；程序集、菜单所用程序集版本和两处诊断版本一致为 `0.11.10.0`。

## 构建与运行

使用 Windows x64 系统 .NET Framework64 编译器。建议完整 .NET Framework 4.8/4.8.1；最低平台、ARM 仿真和新机器尚未验收。没有 NuGet 安装步骤。

```powershell
& .\windows\src\build.ps1 -OutputDirectory .\build\windows
```

保留生成的 EXE 和同名 `.config`。分享 ZIP 解压到可写目录后自行手动启动。没有安装器、开机自启注册或自动更新；EXE 未 Authenticode 签名，SmartScreen 未现场验证。此次未启动正常应用或替换当前安装。

额度依赖用户独立安装并登录的兼容 `codex.exe app-server`，使用只读 `account/read`（refreshToken=false）及 `account/rateLimits/read`。CLI 发现顺序为 `CODEX_USAGE_MINI_CODEX_PATH`、PATH 的 `codex.exe`及用户本地 OpenAI/Codex/bin 版本目录。浮条依赖可见的兼容 Codex GUI/UI Automation 布局；目标隐藏或识别失败时托盘仍可使用。运行不需要 Node/Python，它们仅用于开发测试。

## 状态与分享

Tibo 公共消息来自 AIHOT，刷新、退避、活动投影和可见卡片冻结规则未改。工具读取外观配置及系统主题；托盘使用浅色背景黑图标、深色背景白图标，程序 PE 图标固定黑色。资源与 0.11.9 逐字节相同。

EXE 旁的 `runtime` 可以包含自有消息缓存/已读、位置偏好、app-server 数据库/日志及诊断；诊断可能含机器路径和窗口几何。新分享 ZIP 不含 runtime、凭证或个人偏好。升级时自行先退出旧程序并备份自己的 `runtime/tibo-news.json` 和 `runtime/ui-placement.json`，必要时只迁移这两份状态，不分享认证、会话、数据库或日志。

## 本轮验证

本轮原生构建证据见 `audit/build-validation.json`；各回归摘要见根目录 `audit/tests-*-validation.json`，执行方法见 [测试说明](tests/README.md)。原有79/46/247夹具及新增22项历史回归保留原字节；另补旧缓存离线迁移与历史长文字绘制证据。所有输入使用合成数据和自身临时状态／控件；不观察或操作真实 Codex，不访问真实账户、令牌、私有接口或网络，不发送全局输入。

125%布局预览属于真实控件离屏渲染，不能声称系统 DPI 切换或 Explorer 实际托盘验收。工作区极小情况下的整体高度限制仍保留。新机器、实际个人数据链路与实际目标窗口跟随未测试。

本轮没有访问 Mac、生成 ICNS 或构建 Mac 程序。当前新增源码按根目录 MIT LICENSE 由 Leo-oo 授权，上游原版权声明保留。双平台源码与发行物的公开发布已获用户授权；Mac输入已取得并静态核验。历史 audit 中未授权／未发布字段保留为当次审计记录。
