#!/bin/zsh
set -eu
cd "$(dirname "$0")"
out="${RELEASE_OUTPUT:-$PWD/dist/release-1.0.2-final}"
[[ ! -e "$out" ]] || { print -u2 '拒绝覆盖已有正式交付目录'; exit 1; }
mkdir -p "$out/build" "$out/Codex额度条.app/Contents/MacOS" "$out/Codex额度条.app/Contents/Resources"
app="$out/Codex额度条.app"
for arch in arm64 x86_64; do
 swiftc -DDISTRIBUTION -target "$arch-apple-macos13.0" -swift-version 5 -O -module-cache-path /tmp/codex-usage-swift-cache Sources/*.swift -o "$out/build/CodexUsageMac-$arch"
done
lipo -create "$out/build/CodexUsageMac-arm64" "$out/build/CodexUsageMac-x86_64" -output "$app/Contents/MacOS/CodexUsageMac"
strip -S "$app/Contents/MacOS/CodexUsageMac"
cp Assets/AppIcon.icns "$app/Contents/Resources/"
cp Assets/quota-tray-light.ico Assets/quota-tray-dark.ico "$app/Contents/Resources/"
cp LICENSE.upstream.txt "$app/Contents/Resources/LICENSE.upstream.txt"
cp THIRD-PARTY-NOTICES.txt "$app/Contents/Resources/"
cat > "$app/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleExecutable</key><string>CodexUsageMac</string>
<key>CFBundleIdentifier</key><string>leo.staley.CodexUsageMac</string>
<key>CFBundleName</key><string>Codex额度条</string>
<key>CFBundleDisplayName</key><string>Codex额度条</string>
<key>CFBundleShortVersionString</key><string>1.0.2</string>
<key>CFBundleVersion</key><string>102</string>
<key>CFBundleIconFile</key><string>AppIcon</string>
<key>LSMinimumSystemVersion</key><string>13.0</string>
<key>LSUIElement</key><false/>
<key>NSHighResolutionCapable</key><true/>
<key>NSHumanReadableCopyright</key><string>里奥Leo @staley_leo</string>
</dict></plist>
PLIST
codesign --force --sign - "$app"
codesign --verify --strict --all-architectures "$app"
lipo "$app/Contents/MacOS/CodexUsageMac" -verify_arch arm64 x86_64
