using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WechatStyleScreenshot.Core;
using WechatStyleScreenshot.Services;
using WechatStyleScreenshot.UI;

namespace WechatStyleScreenshot.Tests;

public class OutfitUiFlowTests
{
    [Fact]
    public void ErrorImmediatelyAllowsRetryThroughOverlayButton()
    {
        Exception? failure = null;
        int requests = 0;
        int successes = 0;
        using ManualResetEventSlim finished = new();
        Thread uiThread = new(() =>
        {
            try
            {
                using Bitmap responseImage = new(4, 4);
                using MemoryStream imageStream = new();
                responseImage.Save(imageStream, ImageFormat.Png);
                string successJson = JsonSerializer.Serialize(new
                {
                    data = new[] { new { b64_json = Convert.ToBase64String(imageStream.ToArray()) } }
                });
                using HttpClient client = new(new ImmediateImageHandler(successJson, () => requests++, failFirst: true));
                using AiOutfitPreviewService service = new(client, apiKey: "test-only");
                ScreenshotController controller = new(new ScreenCaptureEngine(), new ClipboardManager(),
                    new OcrService(), _ => { }, service);
                using Bitmap source = new(320, 440);
                ScreenshotOverlayForm overlay = controller.BeginCaptureForTesting(source);
                using System.Windows.Forms.Timer timeout = new() { Interval = 10000 };
                timeout.Tick += (_, _) =>
                {
                    failure = new TimeoutException($"Retry stopped after {requests} requests.");
                    overlay.Close();
                };
                overlay.OutfitStateChangedForTesting += state =>
                {
                    if (state == OutfitPreviewState.Error)
                    {
                        if (!overlay.ClickOutfitButtonForTesting())
                        {
                            failure = new InvalidOperationException("Retry click was rejected while Error was visible.");
                            overlay.BeginInvoke(new Action(overlay.Close));
                        }
                    }
                    else if (state == OutfitPreviewState.Success)
                    {
                        successes++;
                        overlay.BeginInvoke(new Action(overlay.Close));
                    }
                };
                overlay.Shown += (_, _) =>
                {
                    overlay.SetSelectionForTesting(new Rectangle(10, 10, 300, 400));
                    timeout.Start();
                    if (!overlay.ClickOutfitButtonForTesting())
                    {
                        failure = new InvalidOperationException("First click was rejected.");
                        overlay.Close();
                    }
                };
                Application.Run(overlay);
                timeout.Stop();
                controller.Dispose();
            }
            catch (Exception ex) { failure = ex; }
            finally { finished.Set(); }
        });
        uiThread.SetApartmentState(ApartmentState.STA);
        uiThread.Start();
        Assert.True(finished.Wait(TimeSpan.FromSeconds(15)), "UI test thread timed out.");
        Assert.Null(failure);
        Assert.Equal(2, requests);
        Assert.Equal(1, successes);
    }

    [Fact]
    public void SuccessImmediatelyAllowsAnotherClickThroughOverlayButton()
    {
        Exception? failure = null;
        int successes = 0;
        int requests = 0;
        string? originalHash = null;
        List<string> inputHashes = [];
        List<CancellationTokenSource> acquired = [];
        List<CancellationTokenSource> released = [];
        using ManualResetEventSlim finished = new();
        Thread uiThread = new(() =>
        {
            try
            {
                using Bitmap responseImage = new(4, 4);
                using MemoryStream imageStream = new();
                responseImage.Save(imageStream, ImageFormat.Png);
                string responseJson = JsonSerializer.Serialize(new
                {
                    data = new[] { new { b64_json = Convert.ToBase64String(imageStream.ToArray()) } }
                });
                using HttpClient client = new(new ImmediateImageHandler(responseJson, () => requests++));
                using AiOutfitPreviewService service = new(client, apiKey: "test-only");
                ScreenshotController controller = new(new ScreenCaptureEngine(), new ClipboardManager(),
                    new OcrService(), _ => { }, service);
                controller.OutfitGateChangedForTesting += (_, action, request) =>
                {
                    if (action == "ACQUIRED") acquired.Add(request);
                    else released.Add(request);
                };
                controller.OutfitInputHashForTesting += (_, hash) => inputHashes.Add(hash);
                using Bitmap source = new(320, 440);
                ScreenshotOverlayForm overlay = controller.BeginCaptureForTesting(source);
                using System.Windows.Forms.Timer timeout = new() { Interval = 10000 };
                timeout.Tick += (_, _) =>
                {
                    timeout.Stop();
                    failure = new TimeoutException($"UI stopped after {successes} successes and {requests} requests.");
                    overlay.Close();
                };
                overlay.OutfitStateChangedForTesting += state =>
                {
                    if (state != OutfitPreviewState.Success) return;
                    if (controller.OutfitGateBusyForTesting)
                    {
                        failure = new InvalidOperationException("Success became visible before the gate was released.");
                        overlay.BeginInvoke(new Action(overlay.Close));
                        return;
                    }
                    successes++;
                    if (successes == 3)
                    {
                        overlay.BeginInvoke(new Action(overlay.Close));
                        return;
                    }

                    if (!overlay.ClickOutfitButtonForTesting())
                    {
                        failure = new InvalidOperationException($"Click after success {successes} was rejected.");
                        overlay.BeginInvoke(new Action(overlay.Close));
                    }
                };
                overlay.Shown += (_, _) =>
                {
                    overlay.SetSelectionForTesting(new Rectangle(10, 10, 300, 400));
                    using Bitmap original = overlay.CreateOutfitOriginalImageForTesting();
                    originalHash = HashImage(original);
                    timeout.Start();
                    if (!overlay.ClickOutfitButtonForTesting())
                    {
                        failure = new InvalidOperationException("First outfit click was rejected.");
                        overlay.Close();
                    }
                };
                Application.Run(overlay);
                timeout.Stop();
                controller.Dispose();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                finished.Set();
            }
        });
        uiThread.SetApartmentState(ApartmentState.STA);
        uiThread.Start();
        Assert.True(finished.Wait(TimeSpan.FromSeconds(15)), "UI test thread timed out.");
        Assert.Null(failure);
        Assert.Equal(3, requests);
        Assert.Equal(3, successes);
        Assert.Equal(3, acquired.Count);
        Assert.Equal(3, released.Count);
        Assert.Equal(3, acquired.Distinct(ReferenceEqualityComparer.Instance).Count());
        Assert.True(acquired.SequenceEqual(released));
        Assert.Equal(3, inputHashes.Count);
        Assert.All(inputHashes, hash => Assert.Equal(originalHash, hash));
    }

    private static string HashImage(Bitmap image)
    {
        using MemoryStream stream = new();
        image.Save(stream, ImageFormat.Png);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private sealed class ImmediateImageHandler(string responseJson, Action onRequest, bool failFirst = false) : HttpMessageHandler
    {
        private int _requests;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            int requestNumber = ++_requests;
            onRequest();
            if (failFirst && requestNumber == 1)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    Content = new StringContent("{\"error\":{\"code\":\"AccountOverdueError\",\"message\":\"Account overdue\"}}",
                        Encoding.UTF8, "application/json")
                });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        }
    }
}
