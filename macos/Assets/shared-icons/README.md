# Mac 1.0.2 图标素材快照

本目录按 Mac 1.0.2 输入保留 SVG、PNG、iconset、几何和来源记录的原字节。它是历史导出快照；仓库共用的当前规格见 [shared/icons](../../../shared/icons/README.md)，项目许可见根 [LICENSE](../../../LICENSE) 与 [第三方归属说明](../../../THIRD-PARTY-NOTICES.md)。

图形为用户选定的2号连续圆帽圆弧、实心圆形中心及右上方指针。黑色为 `#000000`、白色为 `#FFFFFF`，PNG 使用真实透明背景。SVG 源在本目录，PNG 在 `png/{black,white}/`，标准 Mac iconset 在 `macos/`。几何和来源见 `geometry.json`、`provenance.json`；`asset-manifest.json` 保留的是原导出阶段记录。

Mac 实际运行资源位于上级 Assets 目录：

| 资源 | 用途 |
| --- | --- |
| [AppIcon.icns](../AppIcon.icns) | 正式应用图标 |
| [quota-tray-light.ico](../quota-tray-light.ico) | 浅色菜单栏的黑色标记 |
| [quota-tray-dark.ico](../quota-tray-dark.ico) | 深色菜单栏的白色标记 |

`Sources/TrayArtwork.swift` 实际加载 ICO，`build-release.sh` 将以上三项复制进应用。ICO 格式在这里是 Mac 实现的资源依赖，不代表本目录包含 Windows 代码。

旧 `provenance.json` 的“未生成 ICNS / 未访问 Mac 运行环境”描述属于 SVG/PNG 导出阶段，不能代表后来 Mac 交付；输入另外提供了转换后的 `AppIcon.icns`。本次 Windows 合并只保留并核对资源文件，没有运行 Mac 图标解码或应用。原始外部参考图片没有随源码分发，项目 MIT 不对外部参考图片另行授予权利。

