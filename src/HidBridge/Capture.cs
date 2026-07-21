using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace HidBridge;

internal static class Capture
{
    public static NativeMethods.RECT ResolveRect(WindowInfo? window)
    {
        if (window != null)
        {
            return WindowFinder.GetClientRectOnScreen(window.Handle);
        }
        return new NativeMethods.RECT { Left = 0, Top = 0, Right = NativeScreen.Width, Bottom = NativeScreen.Height };
    }

    public static Bitmap CaptureRect(NativeMethods.RECT rect)
    {
        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0) throw new InvalidOperationException("Invalid capture rectangle (window minimized or off-screen?).");

        var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new System.Drawing.Size(width, height), CopyPixelOperation.SourceCopy);
        return bmp;
    }

    public static string SaveScreenshot(NativeMethods.RECT rect, string outPath)
    {
        using var bmp = CaptureRect(rect);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
        bmp.Save(outPath, ImageFormat.Png);
        return outPath;
    }

    public static string SaveGif(NativeMethods.RECT rect, double seconds, int fps, string outPath, int maxWidth = 640)
    {
        fps = Math.Clamp(fps, 1, 30);
        int frameCount = Math.Max(1, (int)Math.Round(seconds * fps));
        int frameDelayMs = (int)Math.Round(1000.0 / fps);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);

        var sw = Stopwatch.StartNew();
        Image<Rgba32>? gif = null;

        try
        {
            for (int i = 0; i < frameCount; i++)
            {
                long frameStart = sw.ElapsedMilliseconds;

                using var bmp = CaptureRect(rect);
                using var frame = BitmapToImageSharp(bmp);

                if (maxWidth > 0 && frame.Width > maxWidth)
                {
                    int newHeight = (int)Math.Round(frame.Height * (maxWidth / (double)frame.Width));
                    frame.Mutate(x => x.Resize(maxWidth, newHeight));
                }

                var gifMeta = frame.Frames.RootFrame.Metadata.GetGifMetadata();
                gifMeta.FrameDelay = Math.Max(2, frameDelayMs / 10); // GIF delay units are 1/100s

                if (gif == null)
                {
                    gif = frame.Clone();
                }
                else
                {
                    gif.Frames.AddFrame(frame.Frames.RootFrame);
                }

                long elapsed = sw.ElapsedMilliseconds - frameStart;
                int sleepMs = frameDelayMs - (int)elapsed;
                if (sleepMs > 0) Thread.Sleep(sleepMs);
            }

            if (gif == null) throw new InvalidOperationException("No frames captured.");

            var encoder = new GifEncoder();
            gif.SaveAsGif(outPath, encoder);
        }
        finally
        {
            gif?.Dispose();
        }

        return outPath;
    }

    private static Image<Rgba32> BitmapToImageSharp(Bitmap bmp)
    {
        var rect = new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int bytes = data.Stride * bmp.Height;
            var buffer = new byte[bytes];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buffer, 0, bytes);

            using var bgra = SixLabors.ImageSharp.Image.LoadPixelData<Bgra32>(buffer, bmp.Width, bmp.Height);
            return bgra.CloneAs<Rgba32>();
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }
}
