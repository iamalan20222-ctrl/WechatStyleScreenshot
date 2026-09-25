# Changelog

## Unreleased

### Added

- Windows 托盘新增 A/B/C 提示词设置与三家服务商的 API 设置；通用身份/安全约束锁定在代码中，密钥用当前用户 DPAPI 加密保存
- 新增 Qwen Image 3.0 与 OpenAI Image Edit 适配，默认服务商可切换；原火山方舟实现保持可用
- B/C 款改为结构化服装提示词，按服装特征提炼参考图，并为所有款式统一加入选区边界、人物身份、场景连续性与非裸露约束
- Windows “试”按钮新增内嵌款式选择：A 运动风、B 比基尼、C 成人 JK-inspired 穿搭；选款后才生成，再次生成仍使用原图
- Windows 新增 `--outfit-ui-smoke-test`，可用指定图片自动走三次 Overlay/Controller 试衣流程并记录门控、状态及原图哈希
- Windows 截图确认工具栏“试”改为选区内即时生成；支持进度/错误显示、取消、基于原图重试，✓复制生成结果
- AI 穿搭 prompt 身份保持与非裸露约束、成年人和授权确认及云端处理提示
- Windows 本地 Tesseract 5 OCR，支持简体中文和英文
- Windows 截图工具栏 OCR 操作与 `Alt + Shift + A` 直接提取快捷键
- macOS Vision OCR 与 `Option + Shift + A` 快捷键
- OCR 结果自动复制到剪贴板，并在空结果或失败时显示轻量提示
- OCR 依赖与中英模型随 Windows 发布包携带
- OCR 隐私说明：识别在本机完成，截图不会上传

### Fixed

- Windows 程序入口保持 STA，修复剪贴板操作在 MTA 线程抛出 `ThreadStateException`
- Windows 普通截图和 AI 预览使用持久化剪贴板副本、有限重试及读回验证；失败时保留选区供再次确认
- AI 预览复制使用与选区显示相同的 cover 裁剪，避免粘贴结果与屏幕预览比例不一致
- 试衣结果进入 Success 前释放请求门控，避免立即再次点击“试”时第二次请求被拒绝

## 1.0.0

- Windows 支持 `Alt + A` 全局截图
- Windows 支持框选后确认态
- Windows 支持拖动选区和手柄调整大小
- Windows 支持双击确认、右键取消
- Windows 支持自动复制到剪贴板
- Windows 支持托盘菜单和开机启动
- macOS 支持 `Option + A` 调用系统交互截图并复制到剪贴板
- macOS 支持菜单栏常驻和登录启动脚本
