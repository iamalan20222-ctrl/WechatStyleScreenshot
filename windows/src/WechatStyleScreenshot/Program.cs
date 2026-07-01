using WechatStyleScreenshot.Services;

namespace WechatStyleScreenshot;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        StartupManager startupManager = StartupManager.CreateDefault();
        if (args.Contains("--enable-startup", StringComparer.OrdinalIgnoreCase))
        {
            startupManager.Enable();
            return;
        }

        if (args.Contains("--disable-startup", StringComparer.OrdinalIgnoreCase))
        {
            startupManager.Disable();
            return;
        }

        Application.Run(new TrayApplicationContext());
    }
}
