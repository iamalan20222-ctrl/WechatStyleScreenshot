# Contributing

感谢你愿意改进 WechatStyleScreenshot。

## 开发环境

Windows：

- Windows 10/11
- .NET 8 SDK

macOS：

- macOS 12+
- Xcode Command Line Tools

## Windows 验证

```powershell
cd windows
dotnet test .\WechatStyleScreenshot.sln
```

## macOS 验证

```bash
cd macos
./build-macos.sh
open dist/WechatStyleScreenshot.app
```

## 提交建议

- 保持功能极简
- 不引入复杂编辑功能
- 不添加广告、登录、联网依赖
- 优先使用系统原生 API

