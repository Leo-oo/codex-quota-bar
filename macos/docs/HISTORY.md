# Mac 1.0.2 历史交付记录

[原始历史报告](history-1.0.2-original.txt) 按输入字节保留。它描述的是本次公开准备之前的交付状态，其中“MIT及GitHub公开未批准”与 Windows 尚未运行的描述是历史状态；当前许可和发布范围以仓库根 LICENSE、README 和发布说明为准。

历史报告称 323 项既有回归及 21 项新增 Swift 新闻历史回归通过，共 344 项；浅色单周与深色双窗口各 192 项自有宿主事件队列检查通过，共 384 项。这些数量仅来自历史文本，本次输入未包含对应的结构化结果。本次 Windows 合并没有重跑 Mac 构建或测试。

`Sources/SelfHostInputChecks.swift` 明确使用应用本地 `NSEvent` 队列并通过 `NSApp.postEvent` 发送合成事件。因此，384 项不代表物理鼠标输入、CUA 或真实 Codex 窗口验收。报告还记录首次测试遇到自有宿主几何恢复，增加测试宿主稳定等待后复测；应保留这个限制。

记录中的 Universal、macOS 13 目标与 ad-hoc 签名可以由构建脚本核对。Intel/macOS 13 新机器未实测，Apple Developer ID 签名和公证未完成。
