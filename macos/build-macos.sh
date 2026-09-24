#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")" && pwd)"
BUILD_DIR="$ROOT_DIR/build"
DIST_DIR="$ROOT_DIR/dist"
APP_DIR="$DIST_DIR/WechatStyleScreenshot.app"
CONTENTS_DIR="$APP_DIR/Contents"
MACOS_DIR="$CONTENTS_DIR/MacOS"
RESOURCES_DIR="$CONTENTS_DIR/Resources"

rm -rf "$BUILD_DIR" "$DIST_DIR"
mkdir -p "$BUILD_DIR" "$MACOS_DIR" "$RESOURCES_DIR"

swiftc \
  "$ROOT_DIR/src/main.swift" \
  -framework AppKit \
  -framework Carbon \
  -framework Vision \
  -O \
  -o "$MACOS_DIR/WechatStyleScreenshot"

cp "$ROOT_DIR/Info.plist" "$CONTENTS_DIR/Info.plist"

chmod +x "$MACOS_DIR/WechatStyleScreenshot"
codesign --force --deep --sign - "$APP_DIR" >/dev/null 2>&1 || true

ditto -c -k --keepParent "$APP_DIR" "$DIST_DIR/WechatStyleScreenshot-macOS.zip"

echo "Built: $APP_DIR"
echo "Zip:   $DIST_DIR/WechatStyleScreenshot-macOS.zip"
