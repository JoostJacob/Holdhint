#!/bin/bash
# Build Holdhint.app next to this repository. No Xcode required.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

if ! command -v swift >/dev/null 2>&1; then
  echo "Swift is not installed. Install the Xcode Command Line Tools with: xcode-select --install" >&2
  exit 1
fi

# HOLDHINT_BIN and HOLDHINT_APP let scripts/make-release.sh reuse this script
# for a universal build without touching your local Holdhint.app.
if [ -n "${HOLDHINT_BIN:-}" ]; then
  BIN="$HOLDHINT_BIN"
else
  echo "Building Holdhint (release)…"
  swift build -c release
  BIN="$(swift build -c release --show-bin-path)/Holdhint"
fi

APP="${HOLDHINT_APP:-$ROOT/Holdhint.app}"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

cp "$BIN" "$APP/Contents/MacOS/Holdhint"
chmod +x "$APP/Contents/MacOS/Holdhint"

# The shortcut list is copied on its own. The SwiftPM resource bundle is not a
# nested app bundle, and codesign rejects it inside Holdhint.app.
cp "$ROOT/Sources/HoldhintCore/Resources/shortcuts.json" "$APP/Contents/Resources/shortcuts.json"

cat > "$APP/Contents/Info.plist" <<'EOF'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>en</string>
  <key>CFBundleExecutable</key>
  <string>Holdhint</string>
  <key>CFBundleIdentifier</key>
  <string>app.holdhint.macos</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
  <key>CFBundleName</key>
  <string>Holdhint</string>
  <key>CFBundleDisplayName</key>
  <string>Holdhint</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>1.0.0</string>
  <key>CFBundleVersion</key>
  <string>1</string>
  <key>LSApplicationCategoryType</key>
  <string>public.app-category.utilities</string>
  <key>LSMinimumSystemVersion</key>
  <string>14.0</string>
  <key>LSUIElement</key>
  <true/>
  <key>NSHighResolutionCapable</key>
  <true/>
  <key>NSAccessibilityUsageDescription</key>
  <string>Holdhint reads menu titles in the front app so it can list that app’s keyboard shortcuts. It does not record what you type.</string>
  <key>NSHumanReadableCopyright</key>
  <string>Copyright © 2026 Joost Jacob. MIT License.</string>
</dict>
</plist>
EOF

printf 'APPL????' > "$APP/Contents/PkgInfo"
codesign --force --deep --sign - "$APP"
echo "Built $APP"
