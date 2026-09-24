using System.Drawing;

namespace WechatStyleScreenshot.Services;

public sealed class ClipboardManager
{
    private readonly Action<string> _setText;

    public ClipboardManager(Action<string>? setText = null)
    {
        _setText = setText ?? Clipboard.SetText;
    }

    public void SetImage(Image image)
    {
        Clipboard.SetImage(image);
    }

    public bool TrySetText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        _setText(text);
        return true;
    }
}
