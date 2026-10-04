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
(cd "$STAGE" && ditto -c -k --keepParent Holdhint.app "$ROOT/dist/Holdhint-$VERSION.zip")

BG="$STAGE/background.png"
swift "$ROOT/scripts/dmg-background.swift" "$BG"

DMG="dist/Holdhint-$VERSION.dmg"
rm -f "$DMG"

# A .command installer is blocked by the same Gatekeeper dialog as the app,
# and it opens Terminal. The disk image carries a background and a text file
# instead. Finder layout is best-effort; the text file is the fallback.
VOL="Holdhint"
if [ -d "/Volumes/Holdhint" ]; then
  VOL="Holdhint $VERSION"
fi
RW_DIR="$(mktemp -d)"
RW="$RW_DIR/Holdhint-rw.dmg"
hdiutil create -size 128m -fs HFS+ -volname "$VOL" -ov "$RW" >/dev/null
ATTACH="$(hdiutil attach -readwrite -noverify -noautoopen "$RW")"
DEVICE="$(printf '%s\n' "$ATTACH" | awk '/Apple_HFS|Apple_APFS/ {print $1; exit}')"
MOUNT="$(printf '%s\n' "$ATTACH" | sed -n 's/.*\(\/Volumes\/.*\)$/\1/p' | head -1)"
if [ -z "${DEVICE:-}" ] || [ -z "${MOUNT:-}" ]; then
  echo "Could not mount the disk image" >&2
  printf '%s\n' "$ATTACH" >&2
  rm -rf "$STAGE" "$RW_DIR"
  exit 1
fi

cleanup_mount() {
  if [ -n "${DEVICE:-}" ]; then
    hdiutil detach "$DEVICE" >/dev/null 2>&1 || true
  fi
}
trap cleanup_mount EXIT

ditto "$STAGE/Holdhint.app" "$MOUNT/Holdhint.app"
ln -s /Applications "$MOUNT/Applications"
cp "$ROOT/packaging/README.txt" "$MOUNT/README.txt"
mkdir -p "$MOUNT/.background"
cp "$BG" "$MOUNT/.background/background.png"

layout_ok=0
osascript "$ROOT/scripts/dmg-layout.applescript" "$VOL" "$MOUNT" &
layout_pid=$!
for _ in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19 20 21 22 23 24 25 26 27 28 29 30 31 32 33 34 35 36 37 38 39 40; do
  if ! kill -0 "$layout_pid" 2>/dev/null; then
    break
  fi
  sleep 1
done
if kill -0 "$layout_pid" 2>/dev/null; then
  kill "$layout_pid" 2>/dev/null || true
  wait "$layout_pid" 2>/dev/null || true
  echo "Finder layout timed out. Read Me First.txt is still on the disk image."
else
  if wait "$layout_pid"; then
    layout_ok=1
  else
    echo "Finder layout was skipped. Read Me First.txt is still on the disk image."
  fi
fi

sync
trap - EXIT
hdiutil detach "$DEVICE" >/dev/null
DEVICE=""
hdiutil convert "$RW" -format UDZO -ov -o "$RW_DIR/Holdhint-compressed" >/dev/null
mv "$RW_DIR/Holdhint-compressed.dmg" "$DMG"
rm -rf "$RW_DIR" "$STAGE"
if [ "$layout_ok" -eq 1 ]; then
  echo "Built $DMG (with install background) and dist/Holdhint-$VERSION.zip"
else
  echo "Built $DMG and dist/Holdhint-$VERSION.zip"
fi
