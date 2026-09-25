# WechatStyleScreenshot

一个轻量、极简、跨平台的截图工具，目标是做到“像微信截图一样顺手，但更轻、更干净”。

## 功能亮点

- Windows：`Alt + A` 唤醒截图
- Windows：`Alt + Shift + A` 直接框选并提取文字
- Windows：确认工具栏“试”可生成 AI 内衣穿搭预览
- macOS：`Option + A` 唤醒截图
- macOS：`Option + Shift + A` 直接框选并提取文字
- 框选区域后进入确认态
- 选区支持移动和拖拽调整大小
- 双击左键确认截图
- 单击右键退出截图
- 确认后自动复制到剪贴板
- 支持中英文本地 OCR，结果自动复制到剪贴板
- 无广告、无复杂设置、无主窗口
- Windows 支持托盘退出和开机启动
- macOS 支持菜单栏常驻和登录启动脚本

## 平台支持

| 平台 | 状态 | 技术 |
| --- | --- | --- |
| Windows 10/11 | 可用 | C# / .NET 8 / WinForms |
| macOS 12+ | 可构建 | Swift / AppKit / Carbon / Vision |

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
Alt + Shift + A
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
Option + Shift + A
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
10. 点击工具栏“文”可识别选区文字并复制到剪贴板
11. 点击“试”先选择款式；A 运动风、B 泳衣、C JK穿搭始终可用，D-H 仅在“提示词设置”中启用后出现。选中后在当前选区内生成，可再次选款重试，点击“✓”复制当前结果，生成中可按 `Esc` 取消

## AI 穿搭预览

Windows 托盘菜单提供“提示词设置”和“添加 APIKEY”。提示词设置提供固定 A-H 共 8 个槽位：A/B/C 可编辑并恢复默认；D-H 默认禁用，可设置名称和提示词后启用，或再次禁用；“全部恢复默认”会禁用 D-H。人物身份、选区边界和非裸露规则始终从程序内置常量拼接，无法在设置文件中删除。“查看最终提示词”仅供只读预览。设置保存在 `%LOCALAPPDATA%\WechatStyleScreenshot\settings.json`，损坏或删除后自动回退默认款式。

API 设置可分别保存火山方舟、Qwen 和 OpenAI 三组密钥，并选择默认服务。密钥通过 Windows 当前用户 DPAPI 加密后写入 `%LOCALAPPDATA%\WechatStyleScreenshot\secrets.dat`，不写入 `settings.json` 或日志。升级时程序会将当前进程可见的旧 `ARK_API_KEY` 导入加密存储一次；之后 GUI 不再依赖环境变量。若旧用户级环境变量仍存在，需由用户自行决定是否移除。连接测试不上传截图，也不收费；实际鉴权在首次生成时验证。

Qwen 默认使用 `qwen-image-3.0-pro`，可切换 `qwen-image-3.0`；配置时必须填写对应 Region 的 Workspace ID，或选择自定义 HTTPS Base URL。OpenAI 默认使用 `gpt-image-2.5-sunburst`，也可选 `gpt-image-2.5-flare` 或 `gpt-image-2`，通过 Image Edit API 发送选区原图与最终提示词。切换默认服务商不会更改 A-H 的提示词，也不会悄悄回退至别家服务。

Windows 确认工具栏的 `试` 使用所选图像编辑服务。点击后在截图界面内打开款式选择浮层；只有选中已启用的 A-H 款式后才发送请求并在原选区生成，不打开新窗口。点击浮层外可关闭而不发送请求；再次点击“试”仍会重新选款，每次均使用原始截图而非上次生成图。首次使用前在托盘“添加 APIKEY”中保存所选服务商的密钥。旧版火山用户仍可通过下面的环境变量完成一次性加密迁移：

```powershell
$env:ARK_API_KEY = '<your-new-ark-api-key>'
.\publish\WechatStyleScreenshot.exe
```

火山默认模型为 `doubao-seedream-5-0-flash-260915`，模型可在 API 设置窗口修改。点击“试”并选款后会将选区原图上传给当前选择的服务商；请仅处理已成年且有授权的模特图片。原截图及生成图只在内存保留；点击“✓”才将当前预览复制到剪贴板。服务商侧处理、日志与保留依其政策，本程序无法保证服务商不留存。

该功能仅供设计灵感与穿搭参考，非真实打版或合身结果。提示词要求保持身份、脸部、发型、体型、姿势和背景，并禁止裸露及情色内容，但生成模型无法保证像素级身份或身体结构不变。请仅处理已成年且有授权的模特图片。

B/C 款的服装设计方向提炼自用户提供的参考图：B 侧重泳装剪裁、材质与结构，C 侧重学院风搭配、配色与结构。参考图本身不会上传给 AI；只有当前截图选区作为身份和场景输入。生成图只覆盖当前选区，选区外桌面不会被修改。提示词会要求选区内的脸、身材、姿势、背景、光影与构图保持一致，但模型输出仍可能存在偏差。

开发诊断可运行 `--outfit-smoke-test <图片路径>` 仅测试服务，或运行 `--outfit-ui-smoke-test <图片路径>` 自动测试同一 Overlay 内连续三次选择运动风生成。UI 诊断使用指定图片绘制测试窗口，不读取真实桌面；结果与报告保存在图片所在目录的 `ui-results` 中。两种诊断都会真实调用服务并消耗账户额度，且不会输出密钥、图片内容或完整提示词。

AI 结果生成后，可在选区图片内按住鼠标左键临时查看原图，松开后恢复 AI 结果；这仅切换显示，不影响“✓”复制 AI 结果。点击“✓”会将当前选区实际确认的画面复制到剪贴板；AI 预览按选区比例裁剪。复制失败时选区保持打开，可再次点击“✓”。本机剪贴板诊断可运行 `WechatStyleScreenshot.exe --clipboard-smoke-test`，它会写入并读回一张 320×240 的测试图，报告保存在系统临时目录，不调用 AI。

## OCR 文字提取

Windows 按 `Alt + Shift + A` 后拖拽选区，松开鼠标即开始识别；也可在普通截图模式中点击工具栏“文”。macOS 按 `Option + Shift + A` 并使用系统框选截图。中英文识别结果会直接复制到系统剪贴板；未识别到文字时不会覆盖已有剪贴板内容。

## Windows 截图翻译

`Alt + A` 框选后点击工具栏“译”，程序先在本机用 Tesseract 识别文字及位置，再仅将识别出的文字批量发送给 DeepSeek。翻译结果由本机绘制在当前选区内；“✓”复制当前看到的翻译图片，`Ctrl + Z` 撤销翻译。截图像素、桌面内容和 OCR 坐标不会发送给 DeepSeek。翻译过程可用 `Esc` 取消，不会另开窗口。

首次使用时，在托盘“添加 APIKEY”窗口的“翻译 API”页单独配置 DeepSeek Key。该 Key 由 Windows 当前用户 DPAPI 加密保存，不写入 `settings.json`；默认模型为 `deepseek-v4-flash`，也可选择 `deepseek-v4-pro`。可选择简体中文、English、日本語或 한국어。服务商可能记录收到的 OCR 文本，请避免翻译包含敏感信息的截图。

Windows 使用随发布包携带的 Tesseract 5 与 `chi_sim`、`eng` 模型。macOS 使用系统 Vision 框架。两端识别都在本机完成，不调用云端服务。

## 隐私

OCR runs locally and requires no API key. The optional AI outfit preview is a separate cloud feature: clicking “试” opens the enabled A-H style choices; selecting one uploads the selected region. Use only authorized adult-model images. The app does not persist the source image or log its contents; provider-side handling follows the provider policy.

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

OCR dependency notices are documented in [`windows/THIRD-PARTY-NOTICES.md`](windows/THIRD-PARTY-NOTICES.md).
