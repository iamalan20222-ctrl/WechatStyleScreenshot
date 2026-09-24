# WechatStyleScreenshot

一个极简 Windows 桌面截图工具，目标体验是：后台运行，按 `Alt + A` 唤醒，鼠标框选区域，松开后自动复制图片到系统剪贴板，然后直接 `Ctrl + V` 粘贴到微信、PPT、浏览器或文档。

## 功能

- 全局快捷键：`Alt + A`
- OCR 快捷键：`Alt + Shift + A`
- 全屏半透明遮罩
- 鼠标拖拽选择截图区域
- 选区边框和尺寸提示
- 松开鼠标后自动复制到剪贴板
- 本地 Tesseract 5 OCR，支持简体中文和英文
- OCR 结果自动写入剪贴板；空结果不改动原剪贴板
- `Esc` 取消截图
- 无主窗口
- 系统托盘图标
- 托盘右键退出
- 托盘右键切换开机启动
- 命令行开关支持写入或移除开机启动项

## 项目结构

```text
WechatStyleScreenshot/
  src/WechatStyleScreenshot/
    Program.cs                         程序入口，处理启动参数
    AppContext.cs                      托盘、退出菜单、依赖装配
    ScreenshotController.cs            截图流程控制
    Core/SelectionMath.cs              选区几何计算
    Services/HotkeyManager.cs          RegisterHotKey 全局快捷键
    Services/ScreenCaptureEngine.cs    Graphics.CopyFromScreen 截图
    Services/ClipboardManager.cs       Clipboard.SetImage 写入剪贴板
    Services/AiOutfitPreviewService.cs 火山方舟图像编辑 API
    Services/OutfitPromptBuilder.cs    穿搭 prompt 与安全约束
    Services/OutfitPreviewOptions.cs   默认随机穿搭选项
    Services/StartupManager.cs         HKCU Run 开机启动
    UI/ScreenshotOverlayForm.cs        全屏遮罩和鼠标框选
    UI/PreviewResultForm.cs             生成结果预览、复制与保存
  tests/WechatStyleScreenshot.Tests/
    SelectionMathTests.cs
    StartupManagerTests.cs
```

## 依赖

- Windows 10 或 Windows 11
- .NET 8 SDK，用于编译
- .NET 8 Desktop Runtime，用于运行非自包含发布版
- OCR 发布包包含 Tesseract/Leptonica、`chi_sim`/`eng` 模型和 app-local Visual C++ Runtime；用户无需安装 VC++ Runtime 或下载语言模型
- 日常使用 OCR 时不需要网络、账号、API Key 或环境变量

安装 .NET 8 SDK 后，在项目根目录运行：

```powershell
dotnet restore
dotnet test
dotnet run --project .\src\WechatStyleScreenshot\WechatStyleScreenshot.csproj
```

## 生成 exe

框架依赖版，体积小，目标机器需要 .NET 8 Desktop Runtime：

```powershell
dotnet publish .\src\WechatStyleScreenshot\WechatStyleScreenshot.csproj -c Release -r win-x64 --self-contained false -o .\publish
```

自包含发布版（多文件），目标机器不需要预装 .NET 或 VC++ Runtime。OCR 模型和 native DLL 需要与 exe 一起保留：

```powershell
dotnet publish .\src\WechatStyleScreenshot\WechatStyleScreenshot.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o .\publish-self-contained
```

发布后运行：

```powershell
.\publish\WechatStyleScreenshot.exe
```

或：

```powershell
.\publish-self-contained\WechatStyleScreenshot.exe
```

发布目录是可移动的完整应用包；请保留 `tessdata`、`x64` 和 Runtime DLL 文件，不要只复制 EXE。

## 开机启动

程序使用当前用户注册表启动项：

```text
HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run
```

方式一：启动程序后，在托盘图标右键菜单勾选“开机启动”。

方式二：命令行启用或关闭：

```powershell
.\WechatStyleScreenshot.exe --enable-startup
.\WechatStyleScreenshot.exe --disable-startup
```

写入的注册表项名称是：

```text
WechatStyleScreenshot
```

## 使用

1. 启动 `WechatStyleScreenshot.exe`。
2. 按 `Alt + A`。
3. 鼠标拖拽框选区域。
4. 松开鼠标。
5. 在微信、PPT、浏览器或文档中按 `Ctrl + V` 粘贴。
6. 普通截图中点击工具栏“文”提取并复制文字；`Alt + Shift + A` 会在框选松开后立即执行 OCR。
7. 确认工具栏点击“试”后，确认成年与授权再生成；结果可复制、保存或重新生成。

AI 预览需要在启动程序的进程环境中配置 `ARK_API_KEY`，默认使用 Seedream 图像编辑模型；可用 `ARK_MODEL` 覆盖模型名称。应用不会持久化密钥或截图。点击生成会把选区发送至火山方舟，服务商侧数据处理依其政策。提示词包含身份、脸部、发型、比例、姿势、背景保持与非裸露约束，但生成结果不保证完全一致，仅供设计参考。

```powershell
$env:ARK_API_KEY = '<your-new-ark-api-key>'
.\publish\WechatStyleScreenshot.exe
```

## 设计说明

- `HotkeyManager` 使用 Windows API `RegisterHotKey`，不依赖当前焦点窗口。
- `ScreenshotOverlayForm` 覆盖 `SystemInformation.VirtualScreen`，支持多显示器虚拟桌面坐标。
- `ScreenCaptureEngine` 使用 `Graphics.CopyFromScreen`，避免引入大型依赖。
- `ClipboardManager` 使用 `Clipboard.SetImage`，保持和常见 Windows 应用粘贴链路兼容。
- `OcrService` 使用本地 Tesseract 5 `chi_sim+eng`，资源路径相对 `AppContext.BaseDirectory`。
- 发布包带 app-local Visual C++ Runtime DLL，不执行系统级安装；Windows 10/11 的 UCRT 由系统提供。
- `StartupManager` 只写当前用户注册表，不需要管理员权限。

## 注意

- 如果 `Alt + A` 被其他软件占用，托盘会弹出注册失败提示。
- 程序需要在 STA 线程运行，项目入口已配置 `[STAThread]`。
- DPI 感知通过 `app.manifest` 设置为 PerMonitorV2，减少缩放环境下的坐标偏差。
- OCR 识别完全在本机运行；截图与识别文字不会上传，也不会收集。

## 第三方许可

TesseractOCR 与语言模型采用 Apache-2.0 许可。应用包中 Visual C++ Runtime DLL 的再分发受 Microsoft Visual Studio 2022 Runtime 条款约束，详见 [第三方声明](THIRD-PARTY-NOTICES.md)。
