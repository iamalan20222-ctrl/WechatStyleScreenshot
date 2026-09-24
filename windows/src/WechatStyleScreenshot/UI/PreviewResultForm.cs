using System.Drawing;
using WechatStyleScreenshot.Services;

namespace WechatStyleScreenshot.UI;

public sealed class PreviewResultForm : Form
{
    private readonly Bitmap _sourceImage;
    private readonly AiOutfitPreviewService _service;
    private readonly ClipboardManager _clipboard;
    private readonly PictureBox _preview = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(28, 28, 30) };
    private readonly CheckBox _consent = new() { AutoSize = true, Text = "我确认图片中的模特为成年人，且我有权提交此图片进行处理。" };
    private readonly Button _generate = new() { AutoSize = true, Text = "生成预览" };
    private readonly Button _copy = new() { AutoSize = true, Text = "复制" };
    private readonly Button _save = new() { AutoSize = true, Text = "保存" };
    private readonly Button _regenerate = new() { AutoSize = true, Text = "再生成" };
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(790, 0), Text = "点击生成后，所选截图将发送至火山方舟处理。供应商侧日志与保留依其政策；本程序不保存截图。" };
    private readonly CancellationTokenSource _requestCancellation = new();
    private Bitmap? _resultImage;
    private bool _generating;

    public PreviewResultForm(Bitmap sourceImage, AiOutfitPreviewService service, ClipboardManager clipboard)
    {
        _sourceImage = sourceImage;
        _service = service;
        _clipboard = clipboard;
        Text = "AI 穿搭预览";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(560, 480);
        Size = new Size(860, 720);

        Label disclaimer = new()
        {
            AutoSize = true,
            Text = "AI 预览仅供设计灵感与穿搭参考，非真实打版效果。",
            Padding = new Padding(0, 8, 0, 4)
        };
        FlowLayoutPanel actions = new() { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(8) };
        actions.Controls.AddRange([_generate, _copy, _save, _regenerate]);
        FlowLayoutPanel notes = new() { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(10) };
        notes.Controls.Add(_status);
        notes.Controls.Add(_consent);
        notes.Controls.Add(disclaimer);
        Controls.Add(_preview);
        Controls.Add(actions);
        Controls.Add(notes);
        _copy.Enabled = _save.Enabled = _regenerate.Enabled = false;
        _generate.Click += async (_, _) => await GenerateAsync();
        _regenerate.Click += async (_, _) => await GenerateAsync();
        _copy.Click += (_, _) => CopyResult();
        _save.Click += (_, _) => SaveResult();
        FormClosed += (_, _) => _requestCancellation.Cancel();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _requestCancellation.Cancel();
            _requestCancellation.Dispose();
            _preview.Image = null;
            _resultImage?.Dispose();
            _sourceImage.Dispose();
        }
        base.Dispose(disposing);
    }

    private async Task GenerateAsync()
    {
        if (_generating) return;
        if (!_consent.Checked)
        {
            _status.Text = "请先确认模特成年且拥有图片处理授权。";
            return;
        }

        _generating = true;
        SetBusy(true);
        _status.Text = "正在生成，请稍候…";
        try
        {
            OutfitPreviewResult result = await _service.GenerateAsync(_sourceImage, new OutfitPreviewOptions(), _requestCancellation.Token);
            _resultImage?.Dispose();
            _resultImage = result.Image;
            _preview.Image = _resultImage;
            _status.Text = result.Status switch
            {
                OutfitPreviewStatus.Success => "预览已生成。",
                OutfitPreviewStatus.ApiKeyMissing => "未配置试衣接口",
                OutfitPreviewStatus.NoUsableImage => "未返回可用预览图",
                _ => "生成失败，请稍后重试"
            };
            _copy.Enabled = _save.Enabled = _regenerate.Enabled = result.Status == OutfitPreviewStatus.Success;
        }
        catch (OperationCanceledException) { }
        catch
        {
            _status.Text = "生成失败，请稍后重试";
        }
        finally
        {
            _generating = false;
            if (!IsDisposed) SetBusy(false);
        }
    }

    private void CopyResult()
    {
        if (_resultImage is null) return;
        try { _clipboard.SetImage(_resultImage); _status.Text = "预览已复制。"; }
        catch { _status.Text = "复制失败，请稍后重试。"; }
    }

    private void SaveResult()
    {
        if (_resultImage is null) return;
        using SaveFileDialog dialog = new() { Filter = "PNG 图片|*.png", DefaultExt = "png", AddExtension = true, FileName = "outfit-preview.png" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { _resultImage.Save(dialog.FileName, System.Drawing.Imaging.ImageFormat.Png); _status.Text = "预览已保存。"; }
        catch { _status.Text = "保存失败，请检查目标位置后重试。"; }
    }

    private void SetBusy(bool busy)
    {
        _generate.Enabled = !busy;
        _regenerate.Enabled = !busy && _resultImage is not null;
        _copy.Enabled = _save.Enabled = !busy && _resultImage is not null;
        _consent.Enabled = !busy;
        UseWaitCursor = busy;
    }
}
