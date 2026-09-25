using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Security.Cryptography;
using System.Text;
using WechatStyleScreenshot.Core;
using WechatStyleScreenshot.UI;

namespace WechatStyleScreenshot.Services;

internal static class OutfitUiSmokeTest
{
    public static int Run(string imagePath)
    {
        if (!File.Exists(imagePath))
        {
            Console.Error.WriteLine("TEST_IMAGE: NOT_FOUND");
            return 2;
        }

        string apiKey = Environment.GetEnvironmentVariable("ARK_API_KEY")
            ?? Environment.GetEnvironmentVariable("ARK_API_KEY", EnvironmentVariableTarget.User)
            ?? string.Empty;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.Error.WriteLine("RESULT: API_KEY_MISSING; no request sent");
            return 3;
        }

        string outputDirectory = Path.Combine(Path.GetDirectoryName(imagePath)!, "ui-results");
        Directory.CreateDirectory(outputDirectory);
        using StreamWriter report = new(Path.Combine(outputDirectory, "ui-smoke-test-report.txt"),
            append: false, encoding: new UTF8Encoding(false)) { AutoFlush = true };
        using Bitmap source = new(imagePath);
        Rectangle screen = Screen.PrimaryScreen?.Bounds ?? throw new InvalidOperationException("No display is available.");
        double scale = Math.Min(1, Math.Min(screen.Width / (double)source.Width, screen.Height / (double)source.Height));
        Size selectionSize = new(Math.Max(32, (int)Math.Floor(source.Width * scale)),
            Math.Max(32, (int)Math.Floor(source.Height * scale)));
        using Bitmap desktopSnapshot = new(selectionSize.Width, selectionSize.Height);
        using (Graphics graphics = Graphics.FromImage(desktopSnapshot))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(source, new Rectangle(Point.Empty, selectionSize));
        }
        AiOutfitPreviewService service = new(apiKey: apiKey);
        ScreenshotController controller = new(new ScreenCaptureEngine(), new ClipboardManager(),
            new OcrService(), _ => { }, service);
        ScreenshotOverlayForm overlay = controller.BeginCaptureForTesting(desktopSnapshot);
        Dictionary<int, CancellationTokenSource> acquired = [];
        HashSet<int> released = [];
        Dictionary<int, string> inputHashes = [];
        Dictionary<int, int?> httpStatuses = [];
        int exitCode = 1;
        string stage = "OVERLAY_CREATED";

        void Log(string message)
        {
            Console.WriteLine(message);
            report.WriteLine(message);
        }

        Log("OVERLAY_CREATED: true");
        Log($"TEST_IMAGE_SIZE: {source.Width}x{source.Height}");
        Log($"TEST_IMAGE_FILE_SHA256: {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(imagePath)))}");
        Log($"SELECTION_SIZE: {selectionSize.Width}x{selectionSize.Height}");
        controller.OutfitGateChangedForTesting += (number, action, request) =>
        {
            if (action == "ACQUIRED") acquired.Add(number, request);
            else if (action == "RELEASED") released.Add(number);
            Log($"GENERATION_{number}_REQUEST_GATE_{action}: true");
        };
        controller.OutfitInputHashForTesting += (number, hash) =>
        {
            inputHashes.Add(number, hash);
            Log($"GENERATION_{number}_INPUT_HASH: {hash}");
        };
        controller.OutfitResponseForTesting += (number, result) =>
        {
            httpStatuses.Add(number, result.HttpStatusCode);
            Log($"GENERATION_{number}_HTTP: {result.HttpStatusCode?.ToString() ?? "none"}");
            Log($"GENERATION_{number}_PROVIDER_CODE: {result.ProviderCode ?? "none"}");
            Log($"GENERATION_{number}_REQUEST_ID: {result.RequestId ?? "none"}");
            Log($"GENERATION_{number}_RETRY_AFTER: {result.RetryAfterSeconds?.ToString() ?? "none"}");
            Log($"GENERATION_{number}_SERVICE_RESULT: {result.Status}");
        };

        overlay.Shown += async (_, _) =>
        {
            try
            {
                stage = "SELECTION_SET";
                overlay.SetSelectionForTesting(new Rectangle(Point.Empty, selectionSize));
                using Bitmap original = overlay.CreateOutfitOriginalImageForTesting();
                string originalHash = HashImage(original);
                Log("SELECTION_SET: true");
                Log($"ORIGINAL_HASH: {originalHash}");
                List<string> resultHashes = [];

                for (int number = 1; number <= 3; number++)
                {
                    stage = $"GENERATION_{number}_START";
                    Log($"GENERATION_{number}_START: true");
                    List<OutfitPreviewState> states = [];
                    TaskCompletionSource<bool> finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    void OnStateChanged(OutfitPreviewState state)
                    {
                        states.Add(state);
                        Log($"GENERATION_{number}_STATE_{state.ToString().ToUpperInvariant()}: true");
                        if (state == OutfitPreviewState.Success) finished.TrySetResult(true);
                        if (state == OutfitPreviewState.Error) finished.TrySetException(
                            new InvalidOperationException($"Generation {number} entered Error."));
                    }

                    overlay.OutfitStateChangedForTesting += OnStateChanged;
                    try
                    {
                        stage = $"GENERATION_{number}_BUTTON_CLICK";
                        if (!overlay.ClickOutfitButtonForTesting())
                            throw new InvalidOperationException($"Outfit button click {number} did not open the style picker.");
                        if (!overlay.ClickStyleForTesting(OutfitStylePresetType.Sport))
                            throw new InvalidOperationException($"Style selection {number} did not start a request.");
                        Log($"GENERATION_{number}_OUTFIT_BUTTON_TRIGGER: true");
                        stage = $"GENERATION_{number}_WAIT_SUCCESS";
                        await finished.Task.WaitAsync(TimeSpan.FromSeconds(150));
                    }
                    finally
                    {
                        overlay.OutfitStateChangedForTesting -= OnStateChanged;
                    }

                    if (!states.SequenceEqual(new[] { OutfitPreviewState.Preparing, OutfitPreviewState.Generating,
                        OutfitPreviewState.Applying, OutfitPreviewState.Success }))
                        throw new InvalidOperationException($"Generation {number} state sequence was incomplete.");
                    if (!acquired.ContainsKey(number) || !released.Contains(number) || controller.OutfitGateBusyForTesting)
                        throw new InvalidOperationException($"Generation {number} left the request gate busy.");
                    if (httpStatuses.GetValueOrDefault(number) != 200)
                        throw new InvalidOperationException($"Generation {number} did not return HTTP 200.");
                    if (!inputHashes.TryGetValue(number, out string? hash) || hash != originalHash)
                        throw new InvalidOperationException($"Generation {number} did not use the original image.");
                    if (!overlay.HasOutfitResultForTesting)
                        throw new InvalidOperationException($"Generation {number} has no result image.");

                    stage = $"GENERATION_{number}_VALIDATE_IMAGE";
                    using Bitmap result = overlay.CreateOutfitResultImage()!;
                    string resultHash = HashImage(result);
                    if (result.Width < 32 || result.Height < 32 || resultHash == originalHash || resultHashes.Contains(resultHash))
                        throw new InvalidOperationException($"Generation {number} returned an invalid or duplicate image.");
                    resultHashes.Add(resultHash);
                    string resultPath = Path.Combine(outputDirectory, $"ui-result-{number}.png");
                    result.Save(resultPath, ImageFormat.Png);
                    Log($"GENERATION_{number}_IMAGE_VALID: true");
                    Log($"GENERATION_{number}_IMAGE_CHANGED_FROM_ORIGINAL: true");
                    Log($"GENERATION_{number}_RESULT: PASS");
                    Log($"GENERATION_{number}_OUTPUT_FILE: {resultPath}");
                }

                if (acquired.Values.Distinct(ReferenceEqualityComparer.Instance).Count() != 3)
                    throw new InvalidOperationException("CancellationTokenSource was reused between generations.");
                Log("CTS_THREE_UNIQUE: true");
                Log("UI_SMOKE: PASS");
                exitCode = 0;
            }
            catch (Exception ex)
            {
                Log($"FAIL_STAGE: {stage}");
                Log($"CURRENT_STATE: {overlay.OutfitStateForTesting}");
                Log($"REQUEST_GATE_BUSY: {controller.OutfitGateBusyForTesting}");
                Log($"FAIL_TYPE: {ex.GetType().Name}");
                Log($"FAIL_MESSAGE: {ex.Message}");
                Log("UI_SMOKE: FAIL");
            }
            finally
            {
                overlay.Close();
            }
        };

        try { Application.Run(overlay); }
        finally { controller.Dispose(); }
        return exitCode;
    }

    private static string HashImage(Bitmap image)
    {
        using MemoryStream stream = new();
        image.Save(stream, ImageFormat.Png);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}
