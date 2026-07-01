using Microsoft.Win32;

namespace WechatStyleScreenshot.Services;

public interface IStartupRegistry
{
    void SetValue(string name, string value);
    void DeleteValue(string name);
    string? GetValue(string name);
}

public sealed class CurrentUserRunRegistry : IStartupRegistry
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public void SetValue(string name, string value)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true)
            ?? throw new InvalidOperationException("Cannot open current-user startup registry key.");
        key.SetValue(name, value, RegistryValueKind.String);
    }

    public void DeleteValue(string name)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        key?.DeleteValue(name, false);
    }

    public string? GetValue(string name)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
        return key?.GetValue(name) as string;
    }
}

public sealed class StartupManager
{
    public const string AppName = "WechatStyleScreenshot";
    private readonly IStartupRegistry _registry;
    private readonly Func<string> _executablePathProvider;

    public StartupManager(IStartupRegistry registry, Func<string> executablePathProvider)
    {
        _registry = registry;
        _executablePathProvider = executablePathProvider;
    }

    public static StartupManager CreateDefault()
    {
        return new StartupManager(new CurrentUserRunRegistry(), () => Application.ExecutablePath);
    }

    public void Enable()
    {
        _registry.SetValue(AppName, BuildRunCommand(_executablePathProvider()));
    }

    public void Disable()
    {
        _registry.DeleteValue(AppName);
    }

    public bool IsEnabled()
    {
        return string.Equals(_registry.GetValue(AppName), BuildRunCommand(_executablePathProvider()), StringComparison.OrdinalIgnoreCase);
    }

    public static string BuildRunCommand(string executablePath)
    {
        return $"\"{executablePath}\"";
    }
}
