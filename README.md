# Codex Quota Bar / Codex 额度条

非官方 Codex 额度浮条与状态栏工具，由 Leo-oo 维护。项目与 OpenAI 无关联或背书。额度读取依赖用户独立安装并登录兼容 Codex；本工具不会提供额度、充值或重置个人账户。

| 平台 | 当前版本 | 环境与说明 | 发行格式 |
|---|---|---|---|
| Windows x64 | 0.11.10 | [Windows 说明](windows/README.md)；建议 Windows 10/11 与完整 .NET Framework 4.8/4.8.1 | 便携 ZIP，EXE 未签名 |
| macOS Universal | 1.0.2 | [Mac 说明](macos/README.md)；macOS 13.0+，arm64 与 x86_64 | DMG，ad-hoc 签名、未经 Apple 公证 |

安装包见 [Releases](https://github.com/Leo-oo/codex-quota-bar/releases)，校验见 [SHA256SUMS](SHA256SUMS)。Windows 解压到自己的可写目录后手动启动；Mac 使用 DMG 按随包正常安装说明处理。若系统拒绝启动，请先核验来源、版本和哈希，并遵循系统正常安全流程。最低 Windows 版本、ARM仿真、Intel Mac/macOS13新机器及完整Gatekeeper流程未作现场验收。

Windows 提供额度浮条、托盘入口、主题与位置偏好、公共重置事件提示；浮条跟随依赖兼容 Codex GUI/UI Automation 布局。Mac 提供菜单栏与浮条，GUI 跟随目标为 `com.openai.codex`；已有 Accessibility 授权可补充窗口信息，不会主动提权。Mac 可从兼容 ChatGPT.app/Codex 安装发现 CLI，但不会跟随 ChatGPT.app 窗口，也不核验 CLI 与 GUI 是否为同一账户。

额度来自兼容 `codex app-server` 的 `account/read(refreshToken=false)` 与 `account/rateLimits/read`，剩余比例由 `100-usedPercent` 计算，重置时间来自 `resetsAt`。工具沿用所选 CLI 既有认证上下文，显示该 CLI 账户的额度。AIHOT 公共新闻来自独立接口，两者含义不同。外部接口和 Codex UI 变化可能影响兼容性。

当前版本修复公共重置事件缺少发生日期时的跨日历史显示：可靠发生日期优先，缺失时使用可靠确认时间或日期，按 UTC+8 标注时间依据。公共确认不等于个人额度到账，历史变化本身不产生未读红点。Windows 0.11.10还保留旧policy3缓存中的有效历史、跨日消息和未读状态。

运行不需要 Node/Python；构建依赖和测试前提见各平台说明。仓库只纳入源码、原创媒体、第三方许可、合成测试和脱敏审计。不要公开真实runtime、Codex认证或会话、个人偏好、数据库、日志及含个人资料的截图。分享安装包应使用干净发行物。

Windows 六套验证最终选定结果为 **414 PASS / 0 FAIL**：新闻79、图标247、历史22、迁移8、布局12、自有窗口卡片46。首轮失败和对照记录保留；卡片最终验证使用测试自身窗口非激活置顶，125%布局为真实控件离屏渲染。它们不证明真实账户链路、普通窗口层级、实际Codex跟随、Explorer托盘或新机器通过。详见 [验证汇总](audit/validation-summary.json) 与 [测试方法](windows/tests/README.md)。

Mac 交付历史说明记录344项（21+323）和384项通过；384项使用application-local NSEvent合成队列，属于自身宿主检查，不是物理鼠标/CUA验收。本次Windows会话只核对Mac源码、DMG哈希及静态结构，未运行Mac。实际DMG定位到两架构macOS13.0最低版本、ad-hoc签名候选及1.0.2/build102元数据；不将静态检查宣称为公证或Gatekeeper通过。见 [Mac核验范围](audit/macos-publication-validation.md)。

新增代码、文档和原创图标使用 **MIT**，Copyright (c) 2026 Leo-oo，允许商业使用、修改与分发，需保留声明。第三方原许可保留，见 [NOTICE](THIRD-PARTY-NOTICES.md)；AIHOT数据和外部服务不纳入整体MIT。
