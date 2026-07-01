using System.Drawing;

namespace WechatStyleScreenshot.Services;

public sealed class ClipboardManager
{
    public void SetImage(Image image)
    {
        Clipboard.SetImage(image);
    }
}
