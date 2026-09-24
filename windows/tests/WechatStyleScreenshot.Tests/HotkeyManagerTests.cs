using WechatStyleScreenshot.Services;

namespace WechatStyleScreenshot.Tests;

public sealed class HotkeyManagerTests
{
    [Fact]
    public void ScreenshotAndOcrHotkeysUseIndependentIdsAndActions()
    {
        Assert.NotEqual(HotkeyManager.ScreenshotHotkeyId, HotkeyManager.OcrHotkeyId);
        Assert.Equal(HotkeyAction.Screenshot, HotkeyManager.GetActionForId(HotkeyManager.ScreenshotHotkeyId));
        Assert.Equal(HotkeyAction.Ocr, HotkeyManager.GetActionForId(HotkeyManager.OcrHotkeyId));
        Assert.Null(HotkeyManager.GetActionForId(int.MaxValue));
    }
}
