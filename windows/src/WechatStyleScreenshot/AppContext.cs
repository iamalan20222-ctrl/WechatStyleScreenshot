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
        _screenshotController = new ScreenshotController(new ScreenCaptureEngine(), new ClipboardManager());
        _hotkeyManager = new HotkeyManager();
        _hotkeyManager.HotkeyPressed += (_, _) => _screenshotController.BeginCapture();

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
        menu.Items.Add(startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitThread());

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = SystemIcons.Application,
            Text = "Alt + A 截图",
            Visible = true
        };

        try
        {
            _hotkeyManager.RegisterAltA();
        }
        catch (Exception ex)
        {
            _notifyIcon.ShowBalloonTip(5000, "Alt + A 注册失败", ex.Message, ToolTipIcon.Warning);
        }
    }

    protected override void ExitThreadCore()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _hotkeyManager.Dispose();
        base.ExitThreadCore();
    }
}
