using WechatStyleScreenshot.Services;

namespace WechatStyleScreenshot.Tests;

public class StartupManagerTests
{
    [Fact]
    public void EnableWritesQuotedExecutablePath()
    {
        FakeStartupRegistry registry = new();
        StartupManager manager = new(registry, () => @"C:\Tools\Wechat Shot\WechatStyleScreenshot.exe");

        manager.Enable();

        Assert.Equal("\"C:\\Tools\\Wechat Shot\\WechatStyleScreenshot.exe\"", registry.Values[StartupManager.AppName]);
    }

    [Fact]
    public void DisableRemovesStartupValue()
    {
        FakeStartupRegistry registry = new();
        StartupManager manager = new(registry, () => @"C:\Tools\WechatStyleScreenshot.exe");

        manager.Enable();
        manager.Disable();

        Assert.False(registry.Values.ContainsKey(StartupManager.AppName));
    }

    private sealed class FakeStartupRegistry : IStartupRegistry
    {
        public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void SetValue(string name, string value)
        {
            Values[name] = value;
        }

        public void DeleteValue(string name)
        {
            Values.Remove(name);
        }

        public string? GetValue(string name)
        {
            return Values.TryGetValue(name, out string? value) ? value : null;
        }
    }
}
