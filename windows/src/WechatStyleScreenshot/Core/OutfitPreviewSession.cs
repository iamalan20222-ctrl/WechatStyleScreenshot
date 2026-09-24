using System.Drawing;

namespace WechatStyleScreenshot.Core;

public sealed class OutfitPreviewSession : IDisposable
{
    private Bitmap _originalImage;
    private Bitmap? _resultImage;

    public OutfitPreviewState State { get; private set; }
    public bool HasResult => _resultImage is not null;
    public string? ErrorMessage { get; private set; }
    public Bitmap? ResultImage => _resultImage;
    public bool IsBusy => State is OutfitPreviewState.Preparing or OutfitPreviewState.Generating or OutfitPreviewState.Applying;

    public OutfitPreviewSession(Bitmap originalImage)
    {
        ArgumentNullException.ThrowIfNull(originalImage);
        _originalImage = new Bitmap(originalImage);
    }

    public bool TryBegin()
    {
        if (IsBusy) return false;
        ClearResult();
        ErrorMessage = null;
        State = OutfitPreviewState.Preparing;
        return true;
    }

    public Bitmap CreateRequestImage() => new(_originalImage);

    public void MarkGenerating()
    {
        if (State == OutfitPreviewState.Preparing) State = OutfitPreviewState.Generating;
    }

    public void MarkApplying()
    {
        if (State == OutfitPreviewState.Generating) State = OutfitPreviewState.Applying;
    }

    public void Complete(Bitmap resultImage)
    {
        ArgumentNullException.ThrowIfNull(resultImage);
        if (!IsBusy) throw new InvalidOperationException("There is no active outfit preview request.");
        ClearResult();
        _resultImage = resultImage;
        ErrorMessage = null;
        State = OutfitPreviewState.Success;
    }

    public void Fail(string message)
    {
        ClearResult();
        ErrorMessage = message;
        State = OutfitPreviewState.Error;
    }

    public void Cancel()
    {
        ClearResult();
        ErrorMessage = null;
        State = OutfitPreviewState.None;
    }

    public void RestoreOriginal()
    {
        if (IsBusy) return;
        ClearResult();
        ErrorMessage = null;
        State = OutfitPreviewState.None;
    }

    public void ClearError()
    {
        if (State != OutfitPreviewState.Error) return;
        ErrorMessage = null;
        State = OutfitPreviewState.None;
    }

    public void ReplaceOriginal(Bitmap originalImage)
    {
        ArgumentNullException.ThrowIfNull(originalImage);
        if (IsBusy) throw new InvalidOperationException("Cannot change the selection while generating an outfit preview.");
        _originalImage.Dispose();
        _originalImage = new Bitmap(originalImage);
        ClearResult();
        ErrorMessage = null;
        State = OutfitPreviewState.None;
    }

    public Bitmap CreateConfirmationImage() => new(_resultImage ?? _originalImage);

    public void Dispose()
    {
        ClearResult();
        _originalImage.Dispose();
    }

    private void ClearResult()
    {
        _resultImage?.Dispose();
        _resultImage = null;
    }
}
