#!/bin/zsh
set -eu
cd "$(dirname "$0")"
app="${APP_OUTPUT:-$PWD/build/Codex额度条.app}"
flags=()
if [[ "${SELF_HOST_TEST:-0}" == 1 ]]; then flags=(-DSELF_HOST_ONLY); fi
if [[ "${VALIDATION_ONLY:-0}" == 1 ]]; then
 app="$PWD/build/Codex额度条验证.app"
 flags=(-DSELF_HOST_ONLY)
fi
if [[ "${VISUAL_REVIEW:-0}" == 1 ]]; then
 app="$PWD/build/Codex额度条视觉对照.app"
 flags=(-DSELF_HOST_ONLY)
fi
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
swiftc "${flags[@]}" -target arm64-apple-macos13.0 -swift-version 5 -O -module-cache-path /tmp/codex-usage-swift-cache Sources/*.swift -o "$app/Contents/MacOS/CodexUsageMac"
cp Assets/*.ico "$app/Contents/Resources/"
cp LICENSE.upstream.txt "$app/Contents/Resources/LICENSE.upstream.txt"
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
<key>LSMinimumSystemVersion</key><string>13.0</string>
<key>LSUIElement</key><true/>
<key>NSHighResolutionCapable</key><true/>
<key>NSHumanReadableCopyright</key><string>里奥Leo @staley_leo</string>
</dict></plist>
PLIST
if [[ "${VALIDATION_ONLY:-0}" == 1 ]]; then
 /usr/libexec/PlistBuddy -c 'Set :CFBundleIdentifier leo.staley.CodexUsageMac.Validation' "$app/Contents/Info.plist"
 /usr/libexec/PlistBuddy -c 'Set :CFBundleDisplayName Codex额度条验证' "$app/Contents/Info.plist"
fi
if [[ "${VISUAL_REVIEW:-0}" == 1 ]]; then
 /usr/libexec/PlistBuddy -c 'Set :CFBundleIdentifier leo.staley.CodexUsageMac.VisualReview' "$app/Contents/Info.plist"
 /usr/libexec/PlistBuddy -c 'Set :CFBundleName Codex额度条视觉对照' "$app/Contents/Info.plist"
 /usr/libexec/PlistBuddy -c 'Set :CFBundleDisplayName Codex额度条视觉对照' "$app/Contents/Info.plist"
fi
codesign --force --sign - "$app"
printf '%s\n' "$app"
