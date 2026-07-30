using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Conecting.Core
{
    /// <summary>
    /// High-Performance Desktop Screen Capture Engine (GDI / Win32 API).
    /// Captures 24bpp RGB screen buffer with dynamic encoder compression and desktop context switching.
    /// </summary>
    public static class DesktopCapturer
    {
        private static ImageCodecInfo _jpegEncoder;
        private static EncoderParameters _jpegEncoderParams;
        private static Bitmap _captureBitmap;
        private static Graphics _captureGraphics;
        private static int _lastWidth = 0;
        private static int _lastHeight = 0;
        private static ulong _lastSampleHash = 0;
        private static long _lastForceSendTick = 0;
        private static long _lastDesktopBoundTick = 0;

        static DesktopCapturer()
        {
            _jpegEncoder = GetEncoderInfo("image/jpeg");
            _jpegEncoderParams = new EncoderParameters(1);
            _jpegEncoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 75L);
        }

        public static void SetJpegQuality(long quality)
        {
            try
            {
                quality = Math.Max(30L, Math.Min(100L, quality));
                _jpegEncoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
            }
            catch { }
        }

        private static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            ImageCodecInfo[] encoders = ImageCodecInfo.GetImageEncoders();
            for (int i = 0; i < encoders.Length; i++)
            {
                if (encoders[i].MimeType == mimeType) return encoders[i];
            }
            return null;
        }

        public static bool HasScreenChanged(Bitmap bitmap)
        {
            long now = Environment.TickCount;
            if (now - _lastForceSendTick > 120)
            {
                _lastForceSendTick = now;
                return true;
            }

            try
            {
                int w = bitmap.Width;
                int h = bitmap.Height;
                BitmapData data = bitmap.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                ulong hash = 14695981039346656037ul;

                unsafe
                {
                    byte* ptr = (byte*)data.Scan0.ToPointer();
                    int stride = data.Stride;
                    int stepY = Math.Max(1, h / 16);
                    int stepX = Math.Max(1, w / 16);

                    for (int y = 0; y < 16; y++)
                    {
                        byte* row = ptr + (y * stepY * stride);
                        for (int x = 0; x < 16; x++)
                        {
                            int offset = x * stepX * 3;
                            uint val = (uint)(row[offset] | (row[offset + 1] << 8) | (row[offset + 2] << 16));
                            hash = (hash ^ val) * 1099511628211ul;
                        }
                    }
                }

                bitmap.UnlockBits(data);

                if (hash != _lastSampleHash)
                {
                    _lastSampleHash = hash;
                    _lastForceSendTick = now;
                    return true;
                }
                return false;
            }
            catch
            {
                return true;
            }
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr OpenDesktop(string lpszDesktop, uint dwFlags, bool fInherit, uint dwDesiredAccess);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesiredAccess);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetThreadDesktop(IntPtr hDesktop);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool CloseDesktop(IntPtr hDesktop);

        public const uint MAXIMUM_ALLOWED = 0x02000000;
        public const uint DESKTOP_READOBJECTS = 0x0001;
        public const uint DESKTOP_WRITEOBJECTS = 0x0080;
        public const uint DESKTOP_SWITCHDESKTOP = 0x0100;

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        private static IntPtr _lastBoundDesktop = IntPtr.Zero;
        public static void EnsureInputDesktopBound()
        {
            try
            {
                long now = Environment.TickCount;
                if (now - _lastDesktopBoundTick < 250) return;
                _lastDesktopBoundTick = now;

                IntPtr hInputDesktop = OpenInputDesktop(0, false, MAXIMUM_ALLOWED);
                if (hInputDesktop == IntPtr.Zero)
                {
                    hInputDesktop = OpenDesktop("winlogon", 0, false, MAXIMUM_ALLOWED);
                }
                if (hInputDesktop == IntPtr.Zero)
                {
                    hInputDesktop = OpenDesktop("default", 0, false, MAXIMUM_ALLOWED);
                }

                if (hInputDesktop != IntPtr.Zero && hInputDesktop != _lastBoundDesktop)
                {
                    _lastBoundDesktop = hInputDesktop;
                    SetThreadDesktop(hInputDesktop);

                    // Force GDI device context recreation for new desktop
                    if (_captureGraphics != null) { try { _captureGraphics.Dispose(); } catch { } _captureGraphics = null; }
                    if (_captureBitmap != null) { try { _captureBitmap.Dispose(); } catch { } _captureBitmap = null; }
                }
                else if (hInputDesktop != IntPtr.Zero && hInputDesktop == _lastBoundDesktop)
                {
                    CloseDesktop(hInputDesktop);
                }
            }
            catch { }
        }

        private static MemoryStream _sharedMs = new MemoryStream(2 * 1024 * 1024);

        public static byte[] CaptureHighQualityJpeg()
        {
            try
            {
                EnsureInputDesktopBound();

                Rectangle bounds = Screen.PrimaryScreen.Bounds;
                int screenW = bounds.Width;
                int screenH = bounds.Height;

                if (screenW <= 0 || screenH <= 0)
                {
                    screenW = GetSystemMetrics(SM_CXSCREEN);
                    screenH = GetSystemMetrics(SM_CYSCREEN);
                }

                if (screenW <= 0) screenW = 1920;
                if (screenH <= 0) screenH = 1080;

                if (_captureBitmap == null || screenW != _lastWidth || screenH != _lastHeight)
                {
                    if (_captureGraphics != null) _captureGraphics.Dispose();
                    if (_captureBitmap != null) _captureBitmap.Dispose();

                    _lastWidth = screenW;
                    _lastHeight = screenH;
                    _captureBitmap = new Bitmap(screenW, screenH, PixelFormat.Format24bppRgb);
                    _captureGraphics = Graphics.FromImage(_captureBitmap);
                }

                _captureGraphics.CopyFromScreen(0, 0, 0, 0, new Size(screenW, screenH), CopyPixelOperation.SourceCopy);

                if (!HasScreenChanged(_captureBitmap))
                {
                    return null;
                }

                _sharedMs.Position = 0;
                _sharedMs.SetLength(0);
                _captureBitmap.Save(_sharedMs, _jpegEncoder, _jpegEncoderParams);

                int len = (int)_sharedMs.Length;
                byte[] rawBuf = _sharedMs.GetBuffer();

                byte[] outBuf = new byte[len];
                Buffer.BlockCopy(rawBuf, 0, outBuf, 0, len);
                return outBuf;
            }
            catch
            {
                return null;
            }
        }
    }
}
