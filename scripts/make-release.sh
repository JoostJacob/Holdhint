#!/bin/bash
# Build a universal (Apple Silicon + Intel) Holdhint.app and a DMG in dist/.
# Usage: ./scripts/make-release.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

VERSION="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' /dev/stdin <<<"$(sed -n "/<plist/,/<\/plist>/p" scripts/make-app.sh)" 2>/dev/null || echo 1.0.0)"

echo "Building arm64…"
swift build -c release --triple arm64-apple-macosx14.0
echo "Building x86_64…"
swift build -c release --triple x86_64-apple-macosx14.0

mkdir -p .build/universal dist
lipo -create \
  .build/arm64-apple-macosx/release/Holdhint \
  .build/x86_64-apple-macosx/release/Holdhint \
  -output .build/universal/Holdhint

STAGE="$(mktemp -d)"
HOLDHINT_BIN="$ROOT/.build/universal/Holdhint" HOLDHINT_APP="$STAGE/Holdhint.app" ./scripts/make-app.sh
ln -s /Applications "$STAGE/Applications"

DMG="dist/Holdhint-$VERSION.dmg"
rm -f "$DMG"
hdiutil create -volname "Holdhint" -srcfolder "$STAGE" -ov -format UDZO "$DMG" >/dev/null
(cd "$STAGE" && ditto -c -k --keepParent Holdhint.app "$ROOT/dist/Holdhint-$VERSION.zip")
rm -rf "$STAGE"
echo "Built $DMG and dist/Holdhint-$VERSION.zip"
