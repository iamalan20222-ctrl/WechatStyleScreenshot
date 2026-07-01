# WechatStyleScreenshot

一个轻量、极简、跨平台的截图工具，目标是做到“像微信截图一样顺手，但更轻、更干净”。

## 功能亮点

- Windows：`Alt + A` 唤醒截图
- macOS：`Option + A` 唤醒截图
- 框选区域后进入确认态
- 选区支持移动和拖拽调整大小
- 双击左键确认截图
- 单击右键退出截图
- 确认后自动复制到剪贴板
- 无广告、无复杂设置、无主窗口
- Windows 支持托盘退出和开机启动
- macOS 支持菜单栏常驻和登录启动脚本

## 平台支持

| 平台 | 状态 | 技术 |
| --- | --- | --- |
| Windows 10/11 | 可用 | C# / .NET 8 / WinForms |
| macOS 12+ | 可构建 | Swift / AppKit / Carbon |

## 快速开始

### Windows

进入 `windows/`：

```powershell
dotnet test .\WechatStyleScreenshot.sln
dotnet publish .\src\WechatStyleScreenshot\WechatStyleScreenshot.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o .\publish
```

运行：

```powershell
.\publish\WechatStyleScreenshot.exe
```

快捷键：

```text
Alt + A
```

### macOS

进入 `macos/`：

```bash
chmod +x build-and-run.command build-macos.sh install-login-item.sh uninstall-login-item.sh
./build-and-run.command
```

快捷键：

```text
Option + A
```

macOS 首次截图时可能需要授权：

```text
系统设置 > 隐私与安全性 > 屏幕录制
```

如果快捷键无法生效，也检查：

```text
系统设置 > 隐私与安全性 > 辅助功能
```

## Windows 操作说明

1. 启动 `WechatStyleScreenshot.exe`
2. 按 `Alt + A`
3. 鼠标拖拽选择截图区域
4. 松开后进入确认态
5. 拖动选区内部可移动范围
6. 拖动绿色手柄可调整大小
7. 双击左键或点击绿色勾确认
8. 右键、红色叉或 `Esc` 取消
9. 确认后可直接 `Ctrl + V` 粘贴到微信、PPT、浏览器或文档

## 项目结构

```text
.
├─ windows/   Windows 原生截图工具
├─ macos/     macOS 菜单栏截图工具
└─ .github/   CI 和 issue 模板
```

## 构建产物

当前仓库以源码为主。发布包可通过 GitHub Actions 或本地构建生成。

Windows 构建产物：

```text
WechatStyleScreenshot.exe
```

macOS 构建产物：

```text
WechatStyleScreenshot.app
```

## 开机/登录启动

Windows：

```powershell
.\WechatStyleScreenshot.exe --enable-startup
.\WechatStyleScreenshot.exe --disable-startup
```

macOS：

```bash
./install-login-item.sh
./uninstall-login-item.sh
```

## 设计原则

- 快捷键唤醒
- 少 UI
- 少打扰
- 截完即复制
- 尽量使用系统原生能力

## License

MIT

