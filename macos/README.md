# Codex 额度条 — macOS 1.0.2

菜单栏与 Codex 窗口旁的额度工具，显示官方 CLI 返回的剩余额度，并展示 AIHOT 的公开重置活动和最近一条可靠历史。应用为独立第三方工具，非 OpenAI 官方产品。

本目录合并了 Mac 1.0.2 最新交付源码。Swift、构建脚本、运行资源及第三方许可保持交付输入的原字节；本次 Windows 发布准备仅检查和合并文件，没有构建或运行 Mac 应用。

## 使用前提

- macOS 13 或更新版本。正式构建包含 Apple Silicon `arm64` 和 Intel `x86_64`；历史交付说明没有 Intel/macOS 13 新机器的实测记录。
- 本机存在可运行且已登录的 Codex CLI。工具按环境变量 `CODEX_USAGE_MINI_CODEX_PATH`、系统/用户 Applications 中 ChatGPT 或 Codex 自带 CLI、Homebrew 常见目录、启动环境 PATH 的顺序寻找 CLI。
- 额度来自所选 CLI 的 `account/read` 和 `account/rateLimits/read`。工具不校验 CLI 登录账户是否与 GUI 登录账户一致；ChatGPT.app 可提供 CLI。窗口旁入口需要 Codex.app 正在运行且有可见窗口，跟随的 GUI 宿主仅为 `com.openai.codex`。
- 窗口跟随主要使用系统窗口信息。辅助功能仅在系统已授权时作为补充；应用不主动申请或更改系统权限。若需要授权，请通过系统设置手动处理。

安装与普通使用见 [安装说明](RELEASE-安装说明.txt)。应用保存自己的显示设置和公开消息缓存，不随源码或安装包携带个人账户数据。AIHOT 活动和确认时间不代表个人额度已重置或收到重置卡。

## 构建

需要 macOS 上的 Swift 编译器和 Apple 命令行构建工具。脚本使用 Swift 5 语言模式。以下命令从仓库根目录执行：

```sh
cd macos
zsh build-release.sh
zsh package-release.sh
```

[build-release.sh](build-release.sh) 分别编译 `arm64-apple-macos13.0` 和 `x86_64-apple-macos13.0`，再用 `lipo` 合并。默认应用位于 `dist/release-1.0.2-final/Codex额度条.app`；[package-release.sh](package-release.sh) 将其打包为同目录下的 `Codex额度条-1.0.2-macOS-Universal.dmg`。可用 `RELEASE_OUTPUT` 指定输出目录，脚本拒绝覆盖已有正式输出/打包暂存目录。

[build-app.sh](build-app.sh) 是仅编译 arm64 的开发构建，默认输出 `build/Codex额度条.app`，不能代替 Universal 正式构建。隔离的合成预览可使用：

```sh
VALIDATION_ONLY=1 zsh build-app.sh
"build/Codex额度条验证.app/Contents/MacOS/CodexUsageMac" --preview --fixture=week
```

这个验证构建启用 `SELF_HOST_ONLY`，配合 `--preview` 使用自有窗口和合成数据。开发模式的 `--self-host-live` 会读取真实 CLI 额度，属于另一种范围，不能当作合成预览。

所有上述构建仅使用 ad-hoc 签名。没有 Apple Developer ID 签名或 Apple 公证；签名校验和 Universal 架构校验不等于公证。首次打开请遵循 macOS 的正常安全提示和系统允许流程，由使用者决定是否信任应用。

## 测试与历史验证边界

[test.sh](test.sh) 运行解析、几何/窗口策略、新闻历史、离屏渲染与本地假协议服务检查。它需要 `/usr/bin/python3`，且 `Tests/FakeServer.py` 必须可执行：CLI 定位会跳过不可执行的指定文件并回退其他 CLI，缺少此前提时应停止测试。从本目录运行前先设置并检查这两个测试文件的执行权限：

```sh
chmod u+x build-probes.sh Tests/FakeServer.py
test -x /usr/bin/python3 && test -x Tests/FakeServer.py && test -x build-probes.sh && zsh test.sh
```

满足这些前提时，假协议测试只启动本地 `FakeServer.py`。测试输出位于本目录的 `evidence/`。test.sh 不涵盖真实 Codex GUI 跟随或物理输入；[build-probes.sh](build-probes.sh) 会编译 `Tests/probe.swift`，但不会执行它。`Tests/probe.swift` 与 `Tests/QuotaLiveCheck.swift` 可读取真实 CLI/运行环境，不属于该离线测试命令。

[历史验证说明](docs/HISTORY.md) 保留了交付报告的范围：344 项回归（323+21）及 384 项自有宿主事件队列检查均为历史报告，未在本次 Windows 合并中重跑，输入也没有对应的结构化测试结果。384 项使用应用本地合成 `NSEvent` 队列，不能视为真实 Codex 或物理/CUA 输入验收。

## 文件与许可

- [Sources/](Sources/)：AppKit 应用、CLI 协议、窗口跟随、新闻与隔离验证实现。
- [Tests/](Tests/)：解析、模型、离屏渲染、自有窗口验证及单独诊断源码。
- [Assets/](Assets/)：Mac 应用图标与运行时菜单栏资源。ICO 是本 Mac 实现实际使用的资源格式；`TrayArtwork.swift` 加载两枚 ICO，没有再叠加第二个仪表盘。
- [Assets/shared-icons/README.md](Assets/shared-icons/README.md)：随 Mac 输入保留的素材导出快照。仓库的公共图标规格见 [../shared/icons/README.md](../shared/icons/README.md)。

本项目按根 [MIT LICENSE](../LICENSE) 发布，版权所有者为 Leo-oo。根 [第三方归属说明](../THIRD-PARTY-NOTICES.md) 提供概览；Mac 完整第三方版权及许可分别保存在 [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) 和 [LICENSE.upstream.txt](LICENSE.upstream.txt)，不能以项目 MIT 许可替换这些原声明。
