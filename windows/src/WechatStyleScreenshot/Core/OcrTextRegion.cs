using System.Drawing;

namespace WechatStyleScreenshot.Core;

public sealed record OcrTextRegion(int Id, string Text, Rectangle Bounds, float Confidence);
