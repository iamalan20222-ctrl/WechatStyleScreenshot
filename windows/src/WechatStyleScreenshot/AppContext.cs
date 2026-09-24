using WechatStyleScreenshot.Services;

namespace WechatStyleScreenshot;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly HotkeyManager _hotkeyManager;
    private readonly ScreenshotController _screenshotController;
    private readonly StartupManager _startupManager;
    private readonly NotifyIcon _notifyIcon;

    public TrayApplicationContext()
    {
        _startupManager = StartupManager.CreateDefault();
        _hotkeyManager = new HotkeyManager();
        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Alt + A 截图",
            Visible = true
        };
        _screenshotController = new ScreenshotController(
            new ScreenCaptureEngine(),
            new ClipboardManager(),
            new OcrService(),
            ShowOcrNotification,
            new AiOutfitPreviewService());
        _hotkeyManager.HotkeyPressed += (_, args) => _screenshotController.BeginCapture(args.Action == HotkeyAction.Ocr);

        ToolStripMenuItem startupItem = new("开机启动")
        {
            CheckOnClick = true,
            Checked = _startupManager.IsEnabled()
        };
        startupItem.CheckedChanged += (_, _) =>
        {
            if (startupItem.Checked)
            {
                _startupManager.Enable();
            }
            else
            {
                _startupManager.Disable();
            }
        };

        ContextMenuStrip menu = new();
        menu.Items.Add("Alt + A 截图", null, (_, _) => _screenshotController.BeginCapture());
        menu.Items.Add("Alt + Shift + A 提取文字", null, (_, _) => _screenshotController.BeginCapture(extractTextOnSelection: true));
        menu.Items.Add(startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitThread());

        _notifyIcon.ContextMenuStrip = menu;

        RegisterHotkey(_hotkeyManager.RegisterScreenshotHotkey, "Alt + A");
        RegisterHotkey(_hotkeyManager.RegisterOcrHotkey, "Alt + Shift + A");
    }

    private void RegisterHotkey(Action register, string label)
    {
        try
        {
            register();
        }
        catch (Exception ex)
        {
            _notifyIcon.ShowBalloonTip(5000, $"{label} 注册失败", ex.Message, ToolTipIcon.Warning);
        }
    }

    private void ShowOcrNotification(string message)
    {
        _notifyIcon.ShowBalloonTip(3500, "WechatStyleScreenshot", message, ToolTipIcon.Info);
    }

    protected override void ExitThreadCore()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _hotkeyManager.Dispose();
        _screenshotController.Dispose();
        base.ExitThreadCore();
    }
}
