#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT_DIR"

chmod +x build-macos.sh install-login-item.sh uninstall-login-item.sh
./build-macos.sh
open "$ROOT_DIR/dist/WechatStyleScreenshot.app"

