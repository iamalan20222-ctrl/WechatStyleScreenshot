using System.Diagnostics;
using System.Drawing;
using System.Security.Cryptography;
using System.Text;

namespace WechatStyleScreenshot.Services;

internal static class OutfitSmokeTest
{
    public static async Task<int> RunAsync(string imagePath, OutfitStylePresetType preset = OutfitStylePresetType.Sport,
        int requestCount = 3)
    {
        if (!File.Exists(imagePath))
        {
            Console.Error.WriteLine("TEST_IMAGE: NOT_FOUND");
            return 2;
        }

        string apiKey = Environment.GetEnvironmentVariable("ARK_API_KEY") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.Error.WriteLine("RESULT: API_KEY_MISSING; no request sent");
            return 3;
        }

        string outputDirectory = Path.Combine(Path.GetDirectoryName(imagePath)!,
            preset == OutfitStylePresetType.Bikini ? "results-swimwear" : "results");
        Directory.CreateDirectory(outputDirectory);
        string reportPath = Path.Combine(outputDirectory, "smoke-test-report.txt");
        using StreamWriter report = new(reportPath, append: false, encoding: new UTF8Encoding(false));
        using Bitmap original = new(imagePath);
        string inputHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(imagePath)));
        using AiOutfitPreviewService service = new(apiKey: apiKey);
        bool allSucceeded = true;

        WriteLine($"TEST_IMAGE: {imagePath}");
        WriteLine($"TEST_IMAGE_SIZE: {original.Width}x{original.Height}");
        WriteLine("PROVIDER_SMOKE_TEST: START");

        for (int requestNumber = 1; requestNumber <= requestCount; requestNumber++)
        {
            using CancellationTokenSource requestCancellation = new(TimeSpan.FromMinutes(10));
            DateTimeOffset startedAt = DateTimeOffset.Now;
            Stopwatch stopwatch = Stopwatch.StartNew();
            OutfitPreviewResult result = await service.GenerateAsync(original, new OutfitPreviewOptions(preset), requestCancellation.Token);
            stopwatch.Stop();
            DateTimeOffset endedAt = DateTimeOffset.Now;
            bool receivedImage = result.Status == OutfitPreviewStatus.Success && result.Image is not null;
            OutfitPreviewAttempt? finalFailedAttempt = result.Attempts?.LastOrDefault(attempt => attempt.HttpStatusCode is >= 400);
            int? retryAfter = result.RetryAfterSeconds ?? finalFailedAttempt?.RetryAfterSeconds;
            string? providerCode = result.ProviderCode ?? finalFailedAttempt?.ProviderCode;
            string? requestId = finalFailedAttempt?.RequestId ?? result.RequestId;
            string? safeMessage = result.SafeMessage ?? finalFailedAttempt?.SafeMessage;
            int width = 0, height = 0;
            string outputPath = Path.Combine(outputDirectory, $"result-{requestNumber}.png");

            if (receivedImage)
            {
                width = result.Image!.Width;
                height = result.Image.Height;
                result.Image.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
            }
            else
            {
                allSucceeded = false;
            }

            WriteLine($"REQUEST_NUMBER: {requestNumber}");
            WriteLine($"START_TIME: {startedAt:O}");
            WriteLine($"END_TIME: {endedAt:O}");
            WriteLine($"DURATION_MS: {stopwatch.ElapsedMilliseconds}");
            WriteLine($"HTTP_STATUS: {result.HttpStatusCode?.ToString() ?? "none"}");
            WriteLine($"PROVIDER_CODE: {providerCode ?? "none"}");
            WriteLine($"REQUEST_ID: {requestId ?? "none"}");
            WriteLine($"RETRY_AFTER: {retryAfter?.ToString() ?? "none"}");
            WriteLine($"RETRIES: {result.RetryCount}");
            WriteLine($"SAFE_MESSAGE: {Limit(safeMessage)}");
            WriteLine($"ATTEMPT_SEQUENCE: {string.Join(" -> ", (result.Attempts ?? []).Select(attempt => attempt.HttpStatusCode?.ToString() ?? "none"))}");
            WriteLine($"RESULT: {(receivedImage ? "PASS" : result.Status.ToString())}");
            WriteLine($"IMAGE_RECEIVED: {receivedImage}");
            WriteLine($"IMAGE_WIDTH: {width}");
            WriteLine($"IMAGE_HEIGHT: {height}");
            WriteLine($"OUTPUT_FILE: {(receivedImage ? outputPath : "none")}");
            foreach (OutfitPreviewAttempt attempt in result.Attempts ?? [])
            {
                WriteLine($"ATTEMPT: HTTP={attempt.HttpStatusCode?.ToString() ?? "none"}; CODE={attempt.ProviderCode ?? "none"}; REQUEST_ID={attempt.RequestId ?? "none"}; RETRY_AFTER={attempt.RetryAfterSeconds?.ToString() ?? "none"}; MESSAGE={Limit(attempt.SafeMessage)}");
            }

            result.Image?.Dispose();
            WriteLine(string.Empty);

            if (!StringComparer.Ordinal.Equals(inputHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(imagePath)))))
            {
                WriteLine("INPUT_IMAGE_CHANGED: true");
                allSucceeded = false;
                break;
            }
        }

        WriteLine($"PROVIDER_SMOKE_TEST: {(allSucceeded ? "PASS" : "FAIL")}");
        WriteLine($"REPORT_FILE: {reportPath}");
        return allSucceeded ? 0 : 1;

        void WriteLine(string line)
        {
            Console.WriteLine(line);
            report.WriteLine(line);
            report.Flush();
        }
    }

    private static string Limit(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return "none";
        string safe = string.Concat(message.Where(character => !char.IsControl(character))).Trim();
        return safe.Length <= 300 ? safe : safe[..300];
    }
}
