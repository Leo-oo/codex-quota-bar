[README (4) 22a5c68610f182c6b88b01e33005bc28.md](https://github.com/user-attachments/files/33262287/README.4.22a5c68610f182c6b88b01e33005bc28.md)

# README (4)

# Codex 额度条

我每天会点几十次Codex左下角的头像，查看还剩多少额度😂

于是 vibe coding 了个小工具「Codex 额度条」：
1️⃣ 剩余额度直接显示在左下角，不用反复点头像，瞄一眼就知道
2️⃣ 主动提示 Tibo 的额度重置、重置卡发放消息，什么时候生效、哪些用户适用

做它的初衷很简单：
“少点头像，少点焦虑”

## 功能演示

[功能演示.mp4](%E5%8A%9F%E8%83%BD%E6%BC%94%E7%A4%BA.mp4)

[完整功能设计稿.png](%E5%AE%8C%E6%95%B4%E5%8A%9F%E8%83%BD%E8%AE%BE%E8%AE%A1%E7%A8%BF.png)

## 下载

- [**Windows 0.11.10 · x64 ZIP**](https://github.com/Leo-oo/codex-quota-bar/releases/download/release-2026-10-09/CodexUsageMini-v0.11.10-windows-x64.zip)：建议 Windows 10/11，完整 .NET Framework 4.8/4.8.1。
- [**macOS 1.0.2 · Universal DMG**](https://github.com/Leo-oo/codex-quota-bar/releases/download/release-2026-10-09/CodexQuotaBar-v1.0.2-macOS-Universal.dmg)：macOS 13+，包含 Apple Silicon 与 Intel 版本。

安装步骤与兼容说明：[Windows](windows/README.md) · [macOS](macos/RELEASE-安装说明.txt)

[全部版本](https://github.com/Leo-oo/codex-quota-bar/releases) · [文件校验值](https://github.com/Leo-oo/codex-quota-bar/releases/download/release-2026-10-09/SHA256SUMS.txt)

## 使用须知

请先安装并登录 Codex。额度条会自动查找本机的 Codex 命令行组件（CLI），包括兼容桌面版自带的组件；无法读取额度时，请按安装说明排查。额度以该组件的登录账户为准，可能与桌面版不同。

窗口跟随需要兼容且可见的 Codex 窗口；Mac 不跟随 ChatGPT.app 窗口。外部接口或界面变化可能影响兼容性。公共重置消息不代表个人额度已经重置或收到重置卡。

Windows 程序未签名；Mac 仅有 ad-hoc 签名，未经 Apple 公证。首次打开可能出现安全提示，请核验下载来源与校验值，并遵循系统正常安全流程。

本项目由 Leo-oo 维护，非 OpenAI 官方产品，与 OpenAI 无关联或背书。

## 许可与致谢

感谢 [AIHOT](https://aihot.news/) 提供公共重置消息接口。

[MIT License](LICENSE)。完整上游归属与第三方许可见 [NOTICE](THIRD-PARTY-NOTICES.md)；外部服务与数据不纳入本项目 MIT 许可。
