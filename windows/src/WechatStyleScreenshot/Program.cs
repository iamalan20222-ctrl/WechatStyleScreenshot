using WechatStyleScreenshot.Services;

namespace WechatStyleScreenshot;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (args.Length == 2 && args[0].Equals("--outfit-smoke-test", StringComparison.OrdinalIgnoreCase))
        {
            return OutfitSmokeTest.RunAsync(args[1]).GetAwaiter().GetResult();
        }

        if (args.Length == 3 && args[0].Equals("--outfit-smoke-test", StringComparison.OrdinalIgnoreCase) &&
            args[2].Equals("B", StringComparison.OrdinalIgnoreCase))
        {
            return OutfitSmokeTest.RunAsync(args[1], OutfitStylePresetType.Bikini, 1).GetAwaiter().GetResult();
        }

        if (args.Length == 2 && args[0].Equals("--outfit-ui-smoke-test", StringComparison.OrdinalIgnoreCase))
        {
            return OutfitUiSmokeTest.Run(args[1]);
        }

        if (args.Length == 1 && args[0].Equals("--clipboard-smoke-test", StringComparison.OrdinalIgnoreCase))
        {
            return ClipboardSmokeTest.Run();
        }

        StartupManager startupManager = StartupManager.CreateDefault();
        if (args.Contains("--enable-startup", StringComparer.OrdinalIgnoreCase))
        {
            startupManager.Enable();
            return 0;
        }

        if (args.Contains("--disable-startup", StringComparer.OrdinalIgnoreCase))
        {
            startupManager.Disable();
            return 0;
        }

        Application.Run(new TrayApplicationContext());
        return 0;
    }
}
