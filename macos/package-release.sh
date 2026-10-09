#!/bin/zsh
set -eu
cd "$(dirname "$0")"
out="${RELEASE_OUTPUT:-$PWD/dist/release-1.0.2-final}"
app="$out/Codex额度条.app"
stage="$out/dmg-root"
[[ ! -e "$stage" ]] || { print -u2 '拒绝覆盖已有正式暂存目录'; exit 1; }
codesign --verify --strict --all-architectures "$app"
lipo "$app/Contents/MacOS/CodexUsageMac" -verify_arch arm64 x86_64
mkdir -p "$stage"
ditto "$app" "$stage/Codex额度条.app"
ln -s /Applications "$stage/Applications"
cp RELEASE-安装说明.txt "$stage/安装使用说明.txt"
# All comparisons are scoped to our application. Never recurse through Applications.
diff -qr "$app" "$stage/Codex额度条.app"
hdiutil create -volname 'Codex额度条 1.0.2' -srcfolder "$stage" -format UDZO "$out/Codex额度条-1.0.2-macOS-Universal.dmg"
hdiutil verify "$out/Codex额度条-1.0.2-macOS-Universal.dmg"
