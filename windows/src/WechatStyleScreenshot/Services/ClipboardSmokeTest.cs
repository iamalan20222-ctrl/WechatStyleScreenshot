using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Text;

namespace WechatStyleScreenshot.Services;

internal static class ClipboardSmokeTest
{
    public static int Run()
    {
        string reportPath = Path.Combine(Path.GetTempPath(), "WechatStyleScreenshot-clipboard-smoke.txt");
        using StreamWriter report = new(reportPath, append: false, encoding: new UTF8Encoding(false));
        void Log(string line)
        {
            Console.WriteLine(line);
            report.WriteLine(line);
            report.Flush();
        }

        Log($"THREAD_APARTMENT: {Thread.CurrentThread.GetApartmentState()}");
        using TextWriterTraceListener diagnostics = new(report);
        Trace.Listeners.Add(diagnostics);
        Trace.AutoFlush = true;

        bool written;
        using (Bitmap image = new(320, 240, PixelFormat.Format32bppArgb))
        {
            using (Graphics graphics = Graphics.FromImage(image)) graphics.Clear(Color.Green);
            written = new ClipboardManager().TrySetImage(image);
        }

        Log($"CLIPBOARD_WRITE: {(written ? "PASS" : "FAIL")}");
        bool readback = false;
        if (written)
        {
            try
            {
                using Image? image = Clipboard.ContainsImage() ? Clipboard.GetImage() : null;
                readback = image is { Width: 320, Height: 240 };
            }
            catch (System.Runtime.InteropServices.ExternalException) { }
        }

        Log($"CLIPBOARD_READBACK: {(readback ? "PASS" : "FAIL")}");
        Log("EXPECTED_SIZE: 320x240");
        Log($"REPORT_FILE: {reportPath}");
        Trace.Listeners.Remove(diagnostics);
        return written && readback ? 0 : 1;
    }
}
