# WechatStyleScreenshot for macOS

这是 macOS 版轻量截图工具。它是菜单栏常驻应用，使用 `Option + A` 唤醒 macOS 原生框选截图，并自动复制到剪贴板。

## 功能

- 菜单栏常驻，无主窗口
- 全局快捷键：`Option + A`
- OCR 快捷键：`Option + Shift + A`
- 框选截图后自动复制到剪贴板
- 菜单栏可手动触发截图
- 菜单栏可退出程序
- 支持安装为登录启动项
- 使用 Apple Vision 在本机识别中英文截图文字并复制到剪贴板
- 取消系统框选时不运行 OCR，也不更改剪贴板

## 为什么 Mac 版和 Windows 版不是同一个 exe

Windows 版使用 WinForms、`RegisterHotKey`、`Graphics.CopyFromScreen` 和 Windows 剪贴板。macOS 不支持这些 Windows API，所以 MacBook 需要原生 AppKit/Carbon 版本。

这个 macOS 版使用：

- AppKit 菜单栏应用
- Carbon `RegisterEventHotKey` 注册 `Option + A`
- macOS 系统命令 `screencapture -i -c` 做交互式框选并复制剪贴板

## 构建

在 MacBook 上打开终端，进入本目录：

```bash
chmod +x build-and-run.command build-macos.sh install-login-item.sh uninstall-login-item.sh
./build-and-run.command
```

也可以只构建不启动：

```bash
chmod +x build-macos.sh install-login-item.sh uninstall-login-item.sh
./build-macos.sh
```

构建完成后会生成：

```text
dist/WechatStyleScreenshot.app
dist/WechatStyleScreenshot-macOS.zip
```

## 使用

运行：

```bash
open dist/WechatStyleScreenshot.app
```

然后按：

```text
Option + A
Option + Shift + A
```

`Option + Shift + A` 会调用 macOS 系统框选，把图片暂存到系统临时目录后交由 Vision 本地识别；处理结束或取消时临时文件会删除。识别结果只复制到本机剪贴板，不会上传或收集。

首次截图时，macOS 可能要求授予屏幕录制权限：

```text
系统设置 > 隐私与安全性 > 屏幕录制
```

如果快捷键无法生效，也请检查：

```text
系统设置 > 隐私与安全性 > 辅助功能
```

## 登录启动

安装登录启动项：

```bash
./install-login-item.sh
```

取消登录启动项：

```bash
./uninstall-login-item.sh
```

登录启动使用当前用户的 LaunchAgent：

```text
~/Library/LaunchAgents/com.wechatstylescreenshot.app.plist
```
