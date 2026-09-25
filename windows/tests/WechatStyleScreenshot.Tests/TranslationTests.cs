using System.Drawing;
using System.Net;
using System.Text;
using System.Text.Json;
using WechatStyleScreenshot.Core;
using WechatStyleScreenshot.Services;
using WechatStyleScreenshot.UI;

namespace WechatStyleScreenshot.Tests;

public class TranslationTests
{
    [Fact]
    public void TsvParserReturnsLineBounds()
    {
        string tsv = "level\tpage_num\tblock_num\tpar_num\tline_num\tword_num\tleft\ttop\twidth\theight\tconf\ttext\n" +
            "5\t1\t1\t1\t1\t1\t12\t20\t30\t15\t90\tFile\n" +
            "5\t1\t1\t1\t1\t2\t45\t20\t55\t15\t92\tSettings\n";
        OcrTextRegion region = Assert.Single(OcrRegionParser.Parse(tsv, new Size(120, 80)));
        Assert.Equal("File Settings", region.Text);
        Assert.Equal(new Rectangle(12, 20, 88, 15), region.Bounds);
    }

    [Theory]
    [InlineData("https://github.com")]
    [InlineData("C:\\Users\\admin")]
    [InlineData("ark-1234567890123456789")]
    [InlineData("123.45")]
    public void NonTranslatableTextIsSkipped(string text) => Assert.False(DeepSeekTranslationService.ShouldTranslate(text));

    [Fact]
    public async Task BatchRequestContainsOnlyTextAndMapsOutOfOrderIds()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        CredentialStore credentials = new(Path.Combine(root, "secrets.dat"));
        credentials.SaveTranslationCredential("test-only-key");
        OutfitSettingsStore settings = new(Path.Combine(root, "settings.json"));
        string? requestBody = null;
        using HttpClient client = new(new StubHandler(async request =>
        {
            requestBody = await request.Content!.ReadAsStringAsync();
            string inner = JsonSerializer.Serialize(new { translations = new[] { new { id = 1, text = "保存" }, new { id = 0, text = "设置" } } });
            string body = JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = inner } } } });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
        }));
        DeepSeekTranslationService service = new(credentials, settings, client);
        OcrTextRegion[] regions = [new(0, "Settings", new Rectangle(10, 20, 100, 20), 90),
            new(1, "Save", new Rectangle(20, 60, 50, 20), 92)];
        TranslationResult result = await service.TranslateAsync(regions, "简体中文", CancellationToken.None);
        Assert.Equal(TranslationStatus.Success, result.Status);
        Assert.Equal("设置", result.Translations![0]);
        Assert.Equal("保存", result.Translations[1]);
        Assert.DoesNotContain("Bounds", requestBody);
        Assert.DoesNotContain("base64", requestBody);
        Assert.DoesNotContain("10,20", requestBody);
    }

    [Fact]
    public async Task MissingKeyDoesNotCallProvider()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        bool called = false;
        using HttpClient client = new(new StubHandler(_ => { called = true; throw new Exception(); }));
        DeepSeekTranslationService service = new(new CredentialStore(Path.Combine(root, "secrets.dat")),
            new OutfitSettingsStore(Path.Combine(root, "settings.json")), client);
        TranslationResult result = await service.TranslateAsync([new(0, "Save", new Rectangle(0, 0, 40, 20), 90)], "简体中文", CancellationToken.None);
        Assert.Equal(TranslationStatus.ApiKeyMissing, result.Status);
        Assert.False(called);
    }

    [Fact]
    public async Task RateLimitRetriesAtMostTwice()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        CredentialStore credentials = new(Path.Combine(root, "secrets.dat"));
        credentials.SaveTranslationCredential("test-only-key");
        int calls = 0;
        using HttpClient client = new(new StubHandler(_ =>
        {
            calls++;
            if (calls < 3) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
            string inner = JsonSerializer.Serialize(new { translations = new[] { new { id = 0, text = "保存" } } });
            string body = JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = inner } } } });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }));
        DeepSeekTranslationService service = new(credentials, new OutfitSettingsStore(Path.Combine(root, "settings.json")), client);
        TranslationResult result = await service.TranslateAsync([new(0, "Save", new Rectangle(0, 0, 40, 20), 90)], "简体中文", CancellationToken.None);
        Assert.Equal(TranslationStatus.Success, result.Status);
        Assert.Equal(3, calls);
    }

    [Fact]
    public void TranslationCredentialIsDpapiProtectedAndNotInSettings()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        CredentialStore credentials = new(Path.Combine(root, "secrets.dat"));
        OutfitSettingsStore settings = new(Path.Combine(root, "settings.json"));
        credentials.SaveTranslationCredential("deepseek-test-secret");
        settings.Save(new OutfitAppSettings());
        Assert.True(credentials.HasTranslationCredential());
        Assert.Equal("deepseek-test-secret", credentials.GetTranslationCredential());
        Assert.DoesNotContain("deepseek-test-secret", File.ReadAllText(settings.FilePath));
        Assert.DoesNotContain("deepseek-test-secret", Encoding.UTF8.GetString(File.ReadAllBytes(credentials.FilePath)));
        credentials.DeleteTranslationCredential();
        Assert.False(credentials.HasTranslationCredential());
    }

    [Fact]
    public void TranslationSettingsUseIndependentMaskAndModel()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        CredentialStore credentials = new(Path.Combine(root, "secrets.dat"));
        OutfitSettingsStore settings = new(Path.Combine(root, "settings.json"));
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                using ApiSettingsForm form = new(settings, credentials);
                TextBox key = Descendants(form).OfType<TextBox>().Single(box => box.Name == "TranslationApiKey");
                ComboBox model = Descendants(form).OfType<ComboBox>().Single(box => box.Items.Contains("deepseek-v4-pro"));
                Assert.Equal("deepseek-v4-flash", model.SelectedItem);
                key.Text = "deepseek-test-key";
                Assert.True(form.SaveTranslationSettings());
                Assert.Equal(new string('•', 20), key.Text);
                Assert.Equal("deepseek-test-key", credentials.GetTranslationCredential());
                model.SelectedItem = "deepseek-v4-pro";
                Assert.True(form.SaveTranslationSettings());
                Assert.Equal("deepseek-test-key", credentials.GetTranslationCredential());
                using ApiSettingsForm reopened = new(settings, credentials);
                Assert.Equal("deepseek-v4-pro", Descendants(reopened).OfType<ComboBox>()
                    .Single(box => box.Items.Contains("deepseek-v4-pro")).SelectedItem);
                Assert.Equal(new string('•', 20), Descendants(reopened).OfType<TextBox>()
                    .Single(box => box.Name == "TranslationApiKey").Text);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start(); thread.Join();
        Assert.Null(failure);
    }

    [Fact]
    public void RendererPreservesDimensions()
    {
        using Bitmap original = new(200, 100);
        using Bitmap rendered = ScreenshotTranslationRenderer.Render(original,
            [new(0, "Save", new Rectangle(20, 20, 80, 25), 90)], new Dictionary<int, string> { [0] = "保存" });
        Assert.Equal(original.Size, rendered.Size);
    }

    [Fact]
    public void TranslationButtonUsesOverlayEntryAndUndoRestoresOriginal()
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                using Bitmap source = new(320, 220);
                using ScreenshotOverlayForm overlay = new(new Rectangle(0, 0, 320, 220), source);
                overlay.SetSelectionForTesting(new Rectangle(10, 10, 250, 150));
                int requests = 0;
                overlay.TranslationStartRequested += form => { requests++; return form.TryBeginTranslation(); };
                Assert.True(overlay.ClickTranslationButtonForTesting());
                Assert.Equal(1, requests);
                using Bitmap result = new(250, 150);
                overlay.CompleteTranslation(new Bitmap(result));
                Assert.True(overlay.HasTranslationResultForTesting);
                overlay.ClearTranslationResult();
                Assert.False(overlay.HasTranslationResultForTesting);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start(); thread.Join();
        Assert.Null(failure);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handler(request);
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (Control child in control.Controls)
        {
            yield return child;
            foreach (Control nested in Descendants(child)) yield return nested;
        }
    }
}
