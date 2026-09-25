using System.Drawing;

namespace WechatStyleScreenshot.Services;

public interface IImageEditProvider
{
    Task<OutfitPreviewResult> EditAsync(Bitmap image, string prompt, CancellationToken cancellationToken,
        Action<TimeSpan, OutfitPreviewStatus>? retryScheduled = null);
}

public interface IOutfitGenerationService : IDisposable
{
    Task<OutfitPreviewResult> GenerateAsync(Bitmap source, OutfitPreviewOptions options,
        CancellationToken cancellationToken = default,
        Action<TimeSpan, OutfitPreviewStatus>? retryScheduled = null);
}
