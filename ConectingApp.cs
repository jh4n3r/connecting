using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;

[assembly: AssemblyTitle("Connecting Remote Desktop")]
[assembly: AssemblyDescription("Connecting - Plataforma de Asistencia y Escritorio Remoto")]
[assembly: AssemblyCompany("Connecting")]
[assembly: AssemblyProduct("Connecting Remote Desktop Enterprise")]
[assembly: AssemblyCopyright("Copyright © 2026 Connecting")]
[assembly: AssemblyFileVersion("1.0.1.0")]
[assembly: AssemblyVersion("1.0.1.0")]

namespace Conecting
{
    // =========================================================================
    // PROTOCOLO DE PAQUETES UNIFICADO Y ROBUSTO
    // =========================================================================
    public static class PacketProtocol
    {
        public static bool ReadPacket(NetworkStream stream, out byte pktType, out byte[] payload)
        {
            pktType = 0;
            payload = null;
            try
            {
                byte[] header = new byte[5];
                int rHeader = 0;
                while (rHeader < 5)
                {
                    int r = stream.Read(header, rHeader, 5 - rHeader);
                    if (r <= 0) return false;
                    rHeader += r;
                }

                pktType = header[0];
                int payloadLen = BitConverter.ToInt32(header, 1);
                if (payloadLen < 0 || payloadLen > 10000000) return false;

                payload = new byte[payloadLen];
                int totalRead = 0;
                while (totalRead < payloadLen)
                {
                    int r = stream.Read(payload, totalRead, payloadLen - totalRead);
                    if (r <= 0) return false;
                    totalRead += r;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool SendPacket(NetworkStream stream, byte pktType, byte[] payload)
        {
            try
            {
                if (stream == null || !stream.CanWrite) return false;
                int pLen = payload != null ? payload.Length : 0;
                byte[] pkt = new byte[5 + pLen];
                pkt[0] = pktType;
                BitConverter.GetBytes(pLen).CopyTo(pkt, 1);
                if (pLen > 0) payload.CopyTo(pkt, 5);

                stream.Write(pkt, 0, pkt.Length);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    // =========================================================================
    // INYECCIÓN DE EVENTOS NATIVOS DE RATÓN Y TECLADO (WIN32 DUAL INPUT ENGINE)
    // =========================================================================
    public static class NativeInputInjector
    {
        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern uint MapVirtualKey(uint uCode, uint uMapType);

        public const uint MOUSEEVENTF_MOVE = 0x0001;
        public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP = 0x0004;
        public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
        public const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;

        public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        public const uint KEYEVENTF_KEYUP = 0x0002;

        private static bool isLeftDown = false;
        private static bool isRightDown = false;

        public const uint MOUSEEVENTF_WHEEL = 0x0800;

        public static void ExecuteMouseInput(byte evtType, float normX, float normY)
        {
            try
            {
                if (evtType == 0x06) // SCROLL RUEDA ARRIBA
                {
                    mouse_event(MOUSEEVENTF_WHEEL, 0, 0, 120, UIntPtr.Zero);
                    return;
                }
                else if (evtType == 0x07) // SCROLL RUEDA ABAJO
                {
                    mouse_event(MOUSEEVENTF_WHEEL, 0, 0, unchecked((uint)-120), UIntPtr.Zero);
                    return;
                }

                Rectangle bounds = Screen.PrimaryScreen.Bounds;
                int targetX = (int)(normX * bounds.Width);
                int targetY = (int)(normY * bounds.Height);

                SetCursorPos(targetX, targetY);

                uint absX = (uint)(normX * 65535.0f);
                uint absY = (uint)(normY * 65535.0f);

                uint flags = MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK | MOUSEEVENTF_MOVE;

                if (evtType == 0x02) // CLIC IZQUIERDO ABAJO
                {
                    isLeftDown = true;
                    flags |= MOUSEEVENTF_LEFTDOWN;
                }
                else if (evtType == 0x03) // CLIC IZQUIERDO ARRIBA
                {
                    isLeftDown = false;
                    flags |= MOUSEEVENTF_LEFTUP;
                }
                else if (evtType == 0x04) // CLIC DERECHO ABAJO
                {
                    isRightDown = true;
                    flags |= MOUSEEVENTF_RIGHTDOWN;
                }
                else if (evtType == 0x05) // CLIC DERECHO ARRIBA
                {
                    isRightDown = false;
                    flags |= MOUSEEVENTF_RIGHTUP;
                }
                else if (evtType == 0x01) // MOVIMIENTO / ARRASTRE
                {
                    if (isLeftDown) flags |= MOUSEEVENTF_LEFTDOWN;
                    if (isRightDown) flags |= MOUSEEVENTF_RIGHTDOWN;
                }

                mouse_event(flags, absX, absY, 0, UIntPtr.Zero);
            }
            catch { }
        }

        public static void ExecuteKeyboardInput(byte keyCode, bool isDown)
        {
            try
            {
                byte scanCode = (byte)MapVirtualKey(keyCode, 0);
                uint kFlags = isDown ? 0u : KEYEVENTF_KEYUP;
                
                if ((keyCode >= 0x21 && keyCode <= 0x28) || keyCode == 0x2C || keyCode == 0x2D || keyCode == 0x2E || keyCode == 0x5B || keyCode == 0x5C || keyCode == 0xA3 || keyCode == 0xA5)
                {
                    kFlags |= KEYEVENTF_EXTENDEDKEY;
                }

                keybd_event(keyCode, scanCode, kFlags, UIntPtr.Zero);
            }
            catch { }
        }
    }

    // =========================================================================
    // CAPTURA DE PANTALLA DE ALTA DEFINICIÓN (24BPP RGB - CERO PANTALLA NEGRA)
    // =========================================================================
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

        static DesktopCapturer()
        {
            _jpegEncoder = GetEncoderInfo("image/jpeg");
            _jpegEncoderParams = new EncoderParameters(1);
            _jpegEncoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 75L);
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
            if (now - _lastForceSendTick > 1000)
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

        [DllImport("gdi32.dll")]
        public static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        public const uint SRCCOPY = 0x00CC0020;
        public const uint CAPTUREBLT = 0x40000000;

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;

        public static byte[] CaptureHighQualityJpeg()
        {
            try
            {
                Rectangle bounds = Screen.PrimaryScreen.Bounds;
                int width = bounds.Width;
                int height = bounds.Height;

                if (width <= 0 || height <= 0)
                {
                    width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
                    height = GetSystemMetrics(SM_CYVIRTUALSCREEN);
                }

                if (width <= 0 || height <= 0)
                {
                    width = GetSystemMetrics(SM_CXSCREEN);
                    height = GetSystemMetrics(SM_CYSCREEN);
                }

                if (width <= 0) width = 1920;
                if (height <= 0) height = 1080;

                if (_captureBitmap == null || width != _lastWidth || height != _lastHeight)
                {
                    if (_captureGraphics != null) _captureGraphics.Dispose();
                    if (_captureBitmap != null) _captureBitmap.Dispose();

                    _lastWidth = width;
                    _lastHeight = height;
                    _captureBitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                    _captureGraphics = Graphics.FromImage(_captureBitmap);
                }

                IntPtr hdcDest = _captureGraphics.GetHdc();
                IntPtr hdcSrc = GetDC(IntPtr.Zero);
                try
                {
                    BitBlt(hdcDest, 0, 0, width, height, hdcSrc, 0, 0, SRCCOPY | CAPTUREBLT);
                }
                finally
                {
                    _captureGraphics.ReleaseHdc(hdcDest);
                    ReleaseDC(IntPtr.Zero, hdcSrc);
                }

                if (!HasScreenChanged(_captureBitmap)) return null;

                using (MemoryStream ms = new MemoryStream(65536))
                {
                    if (_jpegEncoder != null)
                    {
                        _captureBitmap.Save(ms, _jpegEncoder, _jpegEncoderParams);
                    }
                    else
                    {
                        _captureBitmap.Save(ms, ImageFormat.Jpeg);
                    }
                    return ms.ToArray();
                }
            }
            catch
            {
                return null;
            }
        }
    }

    // =========================================================================
    // ESTRUCTURA Y ADMINISTRADOR DE HISTORIAL Y ALIAS
    // =========================================================================
    public class HistoryItem
    {
        public string Id { get; set; }
        public string Alias { get; set; }
        public string Hostname { get; set; }
    }

    public static class ConnectionHistoryManager
    {
        private static readonly string AppDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ConnectingNodes");
        private static readonly string HistoryFile = Path.Combine(AppDataDirectory, "history.dat");

        public static void SaveRecentSession(string remoteId, string remoteHostname = "", string alias = "")
        {
            try
            {
                if (!Directory.Exists(AppDataDirectory)) Directory.CreateDirectory(AppDataDirectory);
                List<HistoryItem> history = GetRecentSessions();

                HistoryItem existing = history.Find(item => item.Id == remoteId);
                if (existing != null)
                {
                    if (!string.IsNullOrEmpty(remoteHostname)) existing.Hostname = remoteHostname;
                    if (!string.IsNullOrEmpty(alias)) existing.Alias = alias;
                    history.Remove(existing);
                    history.Insert(0, existing);
                }
                else
                {
                    history.Insert(0, new HistoryItem
                    {
                        Id = remoteId,
                        Alias = string.IsNullOrEmpty(alias) ? "" : alias,
                        Hostname = string.IsNullOrEmpty(remoteHostname) ? "PC-REMOTO" : remoteHostname
                    });
                }

                if (history.Count > 8) history = history.GetRange(0, 8);

                List<string> lines = new List<string>();
                foreach (HistoryItem item in history)
                {
                    lines.Add(string.Format("{0}|{1}|{2}", item.Id, item.Alias ?? "", item.Hostname ?? ""));
                }
                File.WriteAllLines(HistoryFile, lines.ToArray());
            }
            catch { }
        }

        public static void UpdateAlias(string remoteId, string newAlias)
        {
            try
            {
                List<HistoryItem> history = GetRecentSessions();
                HistoryItem item = history.Find(i => i.Id == remoteId);
                if (item != null)
                {
                    item.Alias = newAlias.Trim();
                    List<string> lines = new List<string>();
                    foreach (HistoryItem h in history)
                    {
                        lines.Add(string.Format("{0}|{1}|{2}", h.Id, h.Alias ?? "", h.Hostname ?? ""));
                    }
                    File.WriteAllLines(HistoryFile, lines.ToArray());
                }
            }
            catch { }
        }

        public static void RemoveSession(string remoteId)
        {
            try
            {
                List<HistoryItem> history = GetRecentSessions();
                history.RemoveAll(i => i.Id == remoteId);
                List<string> lines = new List<string>();
                foreach (HistoryItem h in history)
                {
                    lines.Add(string.Format("{0}|{1}|{2}", h.Id, h.Alias ?? "", h.Hostname ?? ""));
                }
                File.WriteAllLines(HistoryFile, lines.ToArray());
            }
            catch { }
        }

        public static void ClearAll()
        {
            try
            {
                if (File.Exists(HistoryFile)) File.Delete(HistoryFile);
            }
            catch { }
        }

        public static List<HistoryItem> GetRecentSessions()
        {
            List<HistoryItem> list = new List<HistoryItem>();
            try
            {
                if (File.Exists(HistoryFile))
                {
                    string[] lines = File.ReadAllLines(HistoryFile);
                    foreach (string l in lines)
                    {
                        string clean = l.Trim();
                        if (string.IsNullOrEmpty(clean)) continue;
                        string[] parts = clean.Split('|');
                        if (parts.Length >= 1)
                        {
                            string id = parts[0].Trim();
                            string alias = parts.Length >= 2 ? parts[1].Trim() : "";
                            string host = parts.Length >= 3 ? parts[2].Trim() : "PC-REMOTO";

                            if (!list.Exists(x => x.Id == id))
                            {
                                list.Add(new HistoryItem { Id = id, Alias = alias, Hostname = host });
                            }
                        }
                    }
                }
            }
            catch { }
            return list;
        }
    }

    // =========================================================================
    // PANEL CON BORDES REDONDEADOS Y ESTILO MODERN CARD
    // =========================================================================
    public class ModernCardPanel : Panel
    {
        public int BorderRadius { get; set; }
        public Color BorderColor { get; set; }

        public ModernCardPanel()
        {
            this.BorderRadius = 12;
            this.BorderColor = Color.FromArgb(226, 232, 240);
            this.BackColor = Color.White;
            this.DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            using (GraphicsPath path = GetRoundedPath(rect, BorderRadius))
            {
                using (SolidBrush bgBrush = new SolidBrush(this.BackColor))
                {
                    e.Graphics.FillPath(bgBrush, path);
                }
                using (Pen borderPen = new Pen(BorderColor, 1.5f))
                {
                    e.Graphics.DrawPath(borderPen, path);
                }
            }
        }

        private GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            float r = radius;
            path.AddArc(rect.X, rect.Y, r, r, 180, 90);
            path.AddArc(rect.Right - r, rect.Y, r, r, 270, 90);
            path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);
            path.AddArc(rect.X, rect.Bottom - r, r, r, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class SmoothPictureBox : PictureBox
    {
        public InterpolationMode InterpolationMode { get; set; }

        public SmoothPictureBox()
        {
            this.InterpolationMode = InterpolationMode.HighQualityBilinear;
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            pe.Graphics.InterpolationMode = this.InterpolationMode;
            pe.Graphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;
            pe.Graphics.SmoothingMode = SmoothingMode.HighSpeed;
            base.OnPaint(pe);
        }
    }

    // =========================================================================
    // CONTENEDOR DE TEXTBOX MODERNO ESTILO WEB
    // =========================================================================
    public class ModernInputContainer : Panel
    {
        public int BorderRadius { get; set; }
        public Color BorderColor { get; set; }

        public ModernInputContainer()
        {
            this.BorderRadius = 8;
            this.BorderColor = Color.FromArgb(203, 213, 225);
            this.BackColor = Color.White;
            this.DoubleBuffered = true;
            this.Padding = new Padding(10, 8, 10, 8);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            using (GraphicsPath path = GetRoundedPath(rect, BorderRadius))
            {
                using (SolidBrush bgBrush = new SolidBrush(this.BackColor))
                {
                    e.Graphics.FillPath(bgBrush, path);
                }
                using (Pen borderPen = new Pen(BorderColor, 1.5f))
                {
                    e.Graphics.DrawPath(borderPen, path);
                }
            }
        }

        private GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            float r = radius;
            path.AddArc(rect.X, rect.Y, r, r, 180, 90);
            path.AddArc(rect.Right - r, rect.Y, r, r, 270, 90);
            path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);
            path.AddArc(rect.X, rect.Bottom - r, r, r, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    // =========================================================================
    // BOTÓN MODERNO Y PREMIUM
    // =========================================================================
    public class ModernButton : Button
    {
        public int BorderRadius { get; set; }
        public Color HoverColor { get; set; }
        public Color NormalColor { get; set; }

        private bool isHovered = false;

        public ModernButton()
        {
            this.BorderRadius = 8;
            this.NormalColor = Color.FromArgb(0, 172, 193);
            this.HoverColor = Color.FromArgb(0, 131, 143);
            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 0;
            this.BackColor = NormalColor;
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            this.Cursor = Cursors.Hand;
            this.SetStyle(ControlStyles.Selectable, false);
            this.TabStop = false;

            this.MouseEnter += (s, e) => { isHovered = true; this.Invalidate(); };
            this.MouseLeave += (s, e) => { isHovered = false; this.Invalidate(); };
        }

        protected override bool ShowFocusCues { get { return false; } }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color currentBg = isHovered ? HoverColor : NormalColor;
            using (GraphicsPath path = GetRoundedPath(this.ClientRectangle, BorderRadius))
            {
                using (SolidBrush brush = new SolidBrush(currentBg))
                {
                    g.FillPath(brush, path);
                }
            }

            TextRenderer.DrawText(g, this.Text, this.Font, this.ClientRectangle, this.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            float r = radius;
            path.AddArc(rect.X, rect.Y, r, r, 180, 90);
            path.AddArc(rect.Right - r, rect.Y, r, r, 270, 90);
            path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);
            path.AddArc(rect.X, rect.Bottom - r, r, r, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    // =========================================================================
    // PEER RESOLUTION ENGINE CON PERMANENCIA DE ID Y CLAVE PSK PERSONALIZADA
    // =========================================================================
    public static class PeerResolver
    {
        private static readonly string AppDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ConnectingNodes");
        public static string DefaultRelayHost = "relay.yourdomain.com"; // CAMBIAR AQUÍ: Dominio o IP de tu servidor Relay On-Premise
        public static int DefaultRelayPort = 8443;

        public static string GetRelayServerHost()
        {
            try
            {
                if (!Directory.Exists(AppDataDirectory)) Directory.CreateDirectory(AppDataDirectory);
                string path = Path.Combine(AppDataDirectory, "server_host.dat");
                if (File.Exists(path))
                {
                    string saved = File.ReadAllText(path).Trim();
                    if (!string.IsNullOrEmpty(saved)) return saved;
                }
            }
            catch { }
            return DefaultRelayHost;
        }

        public static void SaveRelayServerHost(string host)
        {
            try
            {
                if (!Directory.Exists(AppDataDirectory)) Directory.CreateDirectory(AppDataDirectory);
                string path = Path.Combine(AppDataDirectory, "server_host.dat");
                File.WriteAllText(path, string.IsNullOrEmpty(host) ? DefaultRelayHost : host.Trim());
            }
            catch { }
        }

        public static string ExtractRawDigitsId(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            StringBuilder sb = new StringBuilder();
            foreach (char c in input)
            {
                if (char.IsDigit(c)) sb.Append(c);
            }
            return sb.ToString();
        }

        public static string GetCustomPsk()
        {
            try
            {
                if (!Directory.Exists(AppDataDirectory)) Directory.CreateDirectory(AppDataDirectory);
                string pskPath = Path.Combine(AppDataDirectory, "custom_psk.dat");
                if (File.Exists(pskPath))
                {
                    string savedPsk = File.ReadAllText(pskPath).Trim();
                    if (!string.IsNullOrEmpty(savedPsk)) return savedPsk;
                }

                string newPsk = GenerateRandomPsk();
                SaveCustomPsk(newPsk);
                return newPsk;
            }
            catch { }
            return "Conn8921";
        }

        public static string GetUserDisplayName()
        {
            try
            {
                if (!Directory.Exists(AppDataDirectory)) Directory.CreateDirectory(AppDataDirectory);
                string namePath = Path.Combine(AppDataDirectory, "user_alias.dat");
                if (File.Exists(namePath))
                {
                    string savedName = File.ReadAllText(namePath).Trim();
                    if (!string.IsNullOrEmpty(savedName)) return savedName;
                }
                string defaultName = Environment.UserName;
                if (string.IsNullOrEmpty(defaultName)) defaultName = "gea";
                SaveUserDisplayName(defaultName);
                return defaultName;
            }
            catch { }
            return "gea";
        }

        public static void SaveUserDisplayName(string name)
        {
            try
            {
                if (!Directory.Exists(AppDataDirectory)) Directory.CreateDirectory(AppDataDirectory);
                string namePath = Path.Combine(AppDataDirectory, "user_alias.dat");
                File.WriteAllText(namePath, string.IsNullOrEmpty(name) ? Environment.UserName : name.Trim());
            }
            catch { }
        }

        public static string GenerateRandomPsk()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            Random rnd = new Random();
            char[] psk = new char[6];
            for (int i = 0; i < 6; i++) psk[i] = chars[rnd.Next(chars.Length)];
            return new string(psk);
        }

        public static void SaveCustomPsk(string psk)
        {
            try
            {
                if (!Directory.Exists(AppDataDirectory)) Directory.CreateDirectory(AppDataDirectory);
                string pskPath = Path.Combine(AppDataDirectory, "custom_psk.dat");
                File.WriteAllText(pskPath, psk.Trim());
            }
            catch { }
        }

        public static string GetPersistentId()
        {
            try
            {
                if (!Directory.Exists(AppDataDirectory)) Directory.CreateDirectory(AppDataDirectory);
                string idPath = Path.Combine(AppDataDirectory, "my_device_id.dat");
                if (File.Exists(idPath))
                {
                    string savedId = File.ReadAllText(idPath).Trim();
                    if (savedId.Length == 9) return savedId;
                }
            }
            catch { }

            string newId = GenerateRandom9DigitId();
            SavePersistentId(newId);
            return newId;
        }

        public static void SavePersistentId(string id)
        {
            try
            {
                if (!Directory.Exists(AppDataDirectory)) Directory.CreateDirectory(AppDataDirectory);
                string idPath = Path.Combine(AppDataDirectory, "my_device_id.dat");
                File.WriteAllText(idPath, id);
            }
            catch { }
        }

        public static string GenerateRandom9DigitId()
        {
            Guid g = Guid.NewGuid();
            int h = Math.Abs(g.GetHashCode());
            string s = h.ToString("D9");
            if (s.Length > 9) s = s.Substring(0, 9);
            while (s.Length < 9) s = s + "7";
            return s;
        }

        public static void RegisterLocalNode(string myId, int myPort, string myPsk)
        {
            try
            {
                if (!Directory.Exists(AppDataDirectory)) Directory.CreateDirectory(AppDataDirectory);
                string peerFilePath = Path.Combine(AppDataDirectory, string.Format("node_{0}.dat", myId));
                using (FileStream fs = new FileStream(peerFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    using (StreamWriter sw = new StreamWriter(fs))
                    {
                        sw.Write(string.Format("127.0.0.1:{0}|{1}", myPort, myPsk));
                    }
                }
            }
            catch { }
        }

        public static TcpClient DiscoverAndConnectPeer(string targetId, string myId, string pskKey, out string remoteHostname, out string errorMsg)
        {
            errorMsg = "";
            remoteHostname = "PC-REMOTO";
            string cleanTargetId = ExtractRawDigitsId(targetId);
            string host = GetRelayServerHost();
            int port = DefaultRelayPort;

            try
            {
                TcpClient client = TrySocketConnect(host, port, myId, cleanTargetId, pskKey, out remoteHostname, 2000);
                if (client != null) return client;
            }
            catch { }

            errorMsg = string.Format("El equipo remoto ID ({0}) no se encuentra en línea o la Clave PSK es incorrecta.", targetId);
            return null;
        }

        private static TcpClient TrySocketConnect(string host, int port, string myId, string targetId, string pskKey, out string remoteHostname, int timeoutMs)
        {
            remoteHostname = "PC-REMOTO";
            try
            {
                TcpClient client = new TcpClient();
                client.NoDelay = true;
                client.SendBufferSize = 262144;
                client.ReceiveBufferSize = 262144;

                IAsyncResult ar = client.BeginConnect(host, port, null, null);
                if (ar.AsyncWaitHandle.WaitOne(timeoutMs) && client.Connected)
                {
                    NetworkStream ns = client.GetStream();
                    ns.ReadTimeout = 30000;
                    ns.WriteTimeout = 3000;

                    byte[] req = Encoding.UTF8.GetBytes(string.Format("CONNECT:{0}:{1}:{2}\n", myId, targetId, pskKey));
                    ns.Write(req, 0, req.Length);
                    ns.Flush();

                    byte[] respBuf = new byte[256];
                    int r = ns.Read(respBuf, 0, 256);
                    if (r > 0)
                    {
                        string resp = Encoding.UTF8.GetString(respBuf, 0, r).Trim();
                        if (resp.StartsWith("ACCEPT_OK"))
                        {
                            string[] p = resp.Split(':');
                            if (p.Length >= 2 && !string.IsNullOrEmpty(p[1]))
                            {
                                remoteHostname = p[1].Trim();
                            }
                            return client;
                        }
                    }
                    client.Close();
                }
            }
            catch { }
            return null;
        }
    }

    // =========================================================================
    // 1. WIDGET FLOTANTE Y PANEL DE CHAT PARA EL HOST
    // =========================================================================
    public class HostSessionFloatingWidget : Form
    {
        private Action onDisconnectAction;
        private NetworkStream stream;
        private Panel chatPanel;
        private RichTextBox txtChatHistory;
        private TextBox txtChatMessage;
        private ModernButton btnSendChat;
        private ModernButton btnToggleChat;
        private ModernButton btnCloseChat;

        public HostSessionFloatingWidget(string remoteClientId, NetworkStream stream, Action onDisconnect)
        {
            this.onDisconnectAction = onDisconnect;
            this.stream = stream;

            this.Text = "Connecting - Sesión Activa";
            this.Size = new Size(360, 52);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.BackColor = Color.White;

            Rectangle screenBounds = Screen.PrimaryScreen.WorkingArea;
            this.Location = new Point(screenBounds.Right - 370, screenBounds.Bottom - 62);

            Panel borderPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Padding = new Padding(10)
            };

            Label lblStatus = new Label
            {
                Text = "SESIÓN ACTIVA",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(16, 185, 129),
                Location = new Point(8, 8),
                AutoSize = true
            };

            Label lblClientInfo = new Label
            {
                Text = string.Format("ID: {0}", remoteClientId),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                Location = new Point(8, 26),
                AutoSize = true
            };

            btnToggleChat = new ModernButton
            {
                Text = "Chat",
                Location = new Point(190, 8),
                Size = new Size(65, 32),
                NormalColor = Color.FromArgb(0, 172, 193),
                HoverColor = Color.FromArgb(0, 131, 143),
                BorderRadius = 6
            };
            btnToggleChat.Click += (s, e) =>
            {
                chatPanel.Visible = !chatPanel.Visible;
                this.Size = chatPanel.Visible ? new Size(360, 230) : new Size(360, 52);
                Rectangle screen = Screen.PrimaryScreen.WorkingArea;
                this.Location = new Point(screen.Right - 370, screen.Bottom - (chatPanel.Visible ? 240 : 62));
            };

            ModernButton btnEnd = new ModernButton
            {
                Text = "Finalizar",
                Location = new Point(262, 8),
                Size = new Size(82, 32),
                NormalColor = Color.FromArgb(239, 68, 68),
                HoverColor = Color.FromArgb(220, 38, 38),
                BorderRadius = 6
            };
            btnEnd.Click += (s, e) =>
            {
                if (onDisconnectAction != null) onDisconnectAction();
                this.Close();
            };

            chatPanel = new Panel
            {
                Location = new Point(10, 48),
                Size = new Size(336, 170),
                BackColor = Color.FromArgb(248, 250, 252),
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false
            };

            Label lblChatTitle = new Label
            {
                Text = "Chat de Soporte",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(6, 6),
                AutoSize = true,
                ForeColor = Color.FromArgb(15, 23, 42)
            };

            btnCloseChat = new ModernButton
            {
                Text = "Cerrar",
                Location = new Point(260, 4),
                Size = new Size(65, 24),
                NormalColor = Color.FromArgb(148, 163, 184),
                HoverColor = Color.FromArgb(100, 116, 139),
                BorderRadius = 4
            };
            btnCloseChat.Click += (s, e) =>
            {
                chatPanel.Visible = false;
                this.Size = new Size(360, 52);
                Rectangle screen = Screen.PrimaryScreen.WorkingArea;
                this.Location = new Point(screen.Right - 370, screen.Bottom - 62);
            };

            txtChatHistory = new RichTextBox
            {
                Location = new Point(6, 30),
                Size = new Size(320, 96),
                ReadOnly = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9F)
            };

            txtChatMessage = new TextBox
            {
                Location = new Point(6, 132),
                Size = new Size(244, 26),
                Font = new Font("Segoe UI", 9.5F)
            };
            txtChatMessage.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SendChatFromHost(); } };

            btnSendChat = new ModernButton
            {
                Text = "Enviar",
                Location = new Point(256, 130),
                Size = new Size(70, 28),
                NormalColor = Color.FromArgb(0, 172, 193),
                HoverColor = Color.FromArgb(0, 131, 143),
                BorderRadius = 6
            };
            btnSendChat.Click += (s, e) => { SendChatFromHost(); };

            chatPanel.Controls.Add(lblChatTitle);
            chatPanel.Controls.Add(btnCloseChat);
            chatPanel.Controls.Add(txtChatHistory);
            chatPanel.Controls.Add(txtChatMessage);
            chatPanel.Controls.Add(btnSendChat);

            borderPanel.Controls.Add(lblStatus);
            borderPanel.Controls.Add(lblClientInfo);
            borderPanel.Controls.Add(btnToggleChat);
            borderPanel.Controls.Add(btnEnd);
            borderPanel.Controls.Add(chatPanel);

            this.Controls.Add(borderPanel);
        }

        public void AppendChatMessage(string senderName, string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { AppendChatMessage(senderName, message); });
                return;
            }

            chatPanel.Visible = true;
            this.Size = new Size(360, 230);
            Rectangle screen = Screen.PrimaryScreen.WorkingArea;
            this.Location = new Point(screen.Right - 370, screen.Bottom - 240);
            txtChatHistory.AppendText(senderName + ": " + message + "\n");
            txtChatHistory.ScrollToCaret();
        }

        private void SendChatFromHost()
        {
            string msg = txtChatMessage.Text.Trim();
            if (string.IsNullOrEmpty(msg) || stream == null) return;

            string myName = PeerResolver.GetUserDisplayName();
            txtChatHistory.AppendText(myName + " (Host): " + msg + "\n");
            txtChatHistory.ScrollToCaret();
            txtChatMessage.Clear();

            byte[] msgBytes = Encoding.UTF8.GetBytes(myName + ": " + msg);
            PacketProtocol.SendPacket(stream, 0x03, msgBytes);
        }
    }

    // =========================================================================
    // MODAL DE SOLICITUD DE CONEXIÓN ENTRANTE
    // =========================================================================
    public class ConnectionRequestForm : Form
    {
        public bool IsAccepted { get; private set; }
        private Label lblRequestingId;
        private ModernButton btnAccept;
        private ModernButton btnReject;

        public ConnectionRequestForm(string remoteClientId)
        {
            this.Text = "Connecting - Solicitud Entrante";
            this.Size = new Size(480, 340);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.White;
            this.TopMost = true;

            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Panel topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 85,
                BackColor = Color.FromArgb(240, 249, 255)
            };

            Label lblTitle = new Label
            {
                Text = "SOLICITUD DE CONEXIÓN ENTRANTE",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 131, 143),
                Location = new Point(20, 16),
                AutoSize = true
            };

            lblRequestingId = new Label
            {
                Text = string.Format("El equipo remoto ID ({0}) solicita tomar control de este ordenador.", remoteClientId),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(51, 65, 85),
                Location = new Point(20, 44),
                AutoSize = true
            };

            topPanel.Controls.Add(lblTitle);
            topPanel.Controls.Add(lblRequestingId);

            GroupBox boxPerms = new GroupBox
            {
                Text = "Permisos concedidos a esta sesión:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Location = new Point(20, 100),
                Size = new Size(424, 120),
                BackColor = Color.White
            };

            CheckBox chkInput = new CheckBox { Text = "Controlar teclado y ratón en tiempo real", Checked = true, Location = new Point(16, 30), AutoSize = true, Font = new Font("Segoe UI", 9.5F) };
            CheckBox chkClipboard = new CheckBox { Text = "Acceder al portapapeles bidireccional", Checked = true, Location = new Point(16, 65), AutoSize = true, Font = new Font("Segoe UI", 9.5F) };

            boxPerms.Controls.Add(chkInput);
            boxPerms.Controls.Add(chkClipboard);

            btnAccept = new ModernButton
            {
                Text = "ACEPTAR",
                Location = new Point(20, 238),
                Size = new Size(200, 46),
                NormalColor = Color.FromArgb(16, 185, 129),
                HoverColor = Color.FromArgb(5, 150, 105),
                BorderRadius = 6
            };
            btnAccept.Click += (s, e) => { IsAccepted = true; this.DialogResult = DialogResult.OK; this.Close(); };

            btnReject = new ModernButton
            {
                Text = "RECHAZAR",
                Location = new Point(244, 238),
                Size = new Size(200, 46),
                NormalColor = Color.FromArgb(239, 68, 68),
                HoverColor = Color.FromArgb(220, 38, 38),
                BorderRadius = 6
            };
            btnReject.Click += (s, e) => { IsAccepted = false; this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.Controls.Add(topPanel);
            this.Controls.Add(boxPerms);
            this.Controls.Add(btnAccept);
            this.Controls.Add(btnReject);
        }
    }

    // =========================================================================
    // MODAL DE ESPERA DEL CLIENTE
    // =========================================================================
    public class ConnectingProgressForm : Form
    {
        private ModernButton btnCancel;

        public ConnectingProgressForm(string remoteId)
        {
            this.Text = "Connecting";
            this.Size = new Size(440, 230);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.White;

            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            PictureBox picIcon = new PictureBox { Size = new Size(48, 48), Location = new Point(24, 24) };
            Bitmap bmp = new Bitmap(48, 48);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen p = new Pen(Color.FromArgb(0, 172, 193), 3.5f)) g.DrawArc(p, 4, 4, 40, 40, 0, 270);
            }
            picIcon.Image = bmp;

            Label lblHeader = new Label { Text = string.Format("Conectando a {0}...", remoteId), Font = new Font("Segoe UI", 12F, FontStyle.Bold), Location = new Point(85, 24), AutoSize = true, ForeColor = Color.FromArgb(15, 23, 42) };
            Label lblSubText = new Label { Text = "Estableciendo conexión en tiempo real...\nEsperando respuesta del equipo remoto.", Font = new Font("Segoe UI", 9.5F), ForeColor = Color.FromArgb(100, 116, 139), Location = new Point(85, 55), AutoSize = true };

            btnCancel = new ModernButton
            {
                Text = "Cancelar",
                Location = new Point(290, 130),
                Size = new Size(115, 38),
                NormalColor = Color.FromArgb(239, 68, 68),
                HoverColor = Color.FromArgb(220, 38, 38),
                BorderRadius = 6
            };
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.Controls.Add(picIcon);
            this.Controls.Add(lblHeader);
            this.Controls.Add(lblSubText);
            this.Controls.Add(btnCancel);
        }
    }

    // =========================================================================
    // 3. VENTANA DEDICADA DE SESIÓN REMOTA CON HOSTNAME EN ENCABEZADO
    // =========================================================================
    public class RemoteSessionForm : Form
    {
        private PictureBox picRemoteDesktop;
        private Panel topToolbar;
        private Panel panelChatDrawer;
        private Panel overlayReconnecting;
        private Label lblReconnectingText;
        private RichTextBox txtChatHistory;
        private TextBox txtChatMessage;
        private ModernButton btnSendChat;
        private ModernButton btnCloseChatDrawer;
        private ModernButton btnFullscreen;
        private ModernButton btnDisconnect;
        private ModernButton btnChat;
        private ModernButton btnQuickActions;
        private ContextMenuStrip menuQuickActions;
        private TcpClient client;
        private NetworkStream stream;
        private bool isSessionActive = true;
        private Thread receiveThread;
        private Thread clipboardThread;
        private string lastClipboardText = "";
        private string remoteTargetId;
        private string remoteHostname;
        private Label lblFps;
        private int _fpsFrameCount = 0;
        private long _lastFpsUpdate = Environment.TickCount;

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private const byte VK_LWIN = 0x5B;
        private const byte VK_CONTROL = 0x11;
        private const byte VK_MENU = 0x12;
        private const byte VK_DELETE = 0x2E;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelKeyboardProc _keyboardProc;
        private IntPtr _keyboardHookId = IntPtr.Zero;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (txtChatMessage.Focused) return base.ProcessCmdKey(ref msg, keyData);
            return true;
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (txtChatMessage.Focused) return base.ProcessDialogKey(keyData);
            return true;
        }

        public RemoteSessionForm(string remoteId, string remoteHostname, TcpClient client)
        {
            this.remoteTargetId = remoteId;
            this.remoteHostname = string.IsNullOrEmpty(remoteHostname) ? "PC-REMOTO" : remoteHostname;
            this.client = client;
            this.client.NoDelay = true;
            this.stream = client.GetStream();

            this.Text = string.Format("Connecting - Sesión Remota: {0} (ID: {1})", this.remoteHostname, remoteId);
            this.Size = new Size(1280, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.WindowState = FormWindowState.Maximized;
            this.BackColor = Color.FromArgb(245, 247, 250);

            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            topToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Color.White,
                Padding = new Padding(12, 6, 12, 6)
            };

            Label lblTitle = new Label
            {
                Text = string.Format("Connecting • Equipo: {0} (ID: {1})", this.remoteHostname, remoteId),
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 131, 143),
                Location = new Point(16, 12),
                AutoSize = true
            };

            btnDisconnect = new ModernButton
            {
                Text = "Finalizar",
                Dock = DockStyle.Right,
                Width = 100,
                NormalColor = Color.FromArgb(239, 68, 68),
                HoverColor = Color.FromArgb(220, 38, 38),
                BorderRadius = 6
            };
            btnDisconnect.Click += (s, e) => { CloseSession(); };

            btnChat = new ModernButton
            {
                Text = "Chat",
                Dock = DockStyle.Right,
                Width = 75,
                NormalColor = Color.FromArgb(241, 245, 249),
                HoverColor = Color.FromArgb(226, 232, 240),
                ForeColor = Color.FromArgb(15, 23, 42),
                BorderRadius = 6
            };

            btnQuickActions = new ModernButton
            {
                Text = "Acciones",
                Dock = DockStyle.Right,
                Width = 90,
                NormalColor = Color.FromArgb(241, 245, 249),
                HoverColor = Color.FromArgb(226, 232, 240),
                ForeColor = Color.FromArgb(15, 23, 42),
                BorderRadius = 6
            };

            ModernButton btnViewMode = new ModernButton
            {
                Text = "Vista",
                Dock = DockStyle.Right,
                Width = 70,
                NormalColor = Color.FromArgb(241, 245, 249),
                HoverColor = Color.FromArgb(226, 232, 240),
                ForeColor = Color.FromArgb(15, 23, 42),
                BorderRadius = 6
            };

            ModernButton btnMainMenu = new ModernButton
            {
                Text = "Menú",
                Dock = DockStyle.Right,
                Width = 70,
                NormalColor = Color.FromArgb(241, 245, 249),
                HoverColor = Color.FromArgb(226, 232, 240),
                ForeColor = Color.FromArgb(15, 23, 42),
                BorderRadius = 6
            };

            BuildQuickActionsMenu();
            BuildViewMenu(btnViewMode);
            BuildMainMenu(btnMainMenu);

            btnQuickActions.Click += (s, e) => { menuQuickActions.Show(btnQuickActions, new Point(0, btnQuickActions.Height)); };

            lblFps = new Label
            {
                Text = "Latencia: ~12 ms",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(16, 185, 129),
                Location = new Point(400, 15),
                AutoSize = true
            };

            topToolbar.Controls.Add(lblTitle);
            topToolbar.Controls.Add(lblFps);
            topToolbar.Controls.Add(btnMainMenu);
            topToolbar.Controls.Add(btnViewMode);
            topToolbar.Controls.Add(btnQuickActions);
            topToolbar.Controls.Add(btnChat);
            topToolbar.Controls.Add(btnDisconnect);

            panelChatDrawer = new Panel
            {
                Size = new Size(300, 450),
                BackColor = Color.White,
                Visible = false,
                Padding = new Padding(12),
                BorderStyle = BorderStyle.FixedSingle
            };

            Label lblChatHeader = new Label { Text = "💬 Chat de Sesión", Font = new Font("Segoe UI", 11F, FontStyle.Bold), Location = new Point(12, 12), AutoSize = true, ForeColor = Color.FromArgb(15, 23, 42) };
            
            btnCloseChatDrawer = new ModernButton
            {
                Text = "✕",
                Location = new Point(260, 8),
                Size = new Size(28, 28),
                NormalColor = Color.FromArgb(239, 68, 68),
                HoverColor = Color.FromArgb(220, 38, 38),
                BorderRadius = 4
            };
            btnCloseChatDrawer.Click += (s, e) =>
            {
                panelChatDrawer.Visible = false;
                this.ActiveControl = picRemoteDesktop;
            };

            txtChatHistory = new RichTextBox { Location = new Point(12, 44), Size = new Size(276, 345), ReadOnly = true, BackColor = Color.FromArgb(248, 250, 252), BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 9.5F) };
            txtChatMessage = new TextBox { Location = new Point(12, 402), Size = new Size(205, 30), Font = new Font("Segoe UI", 10F) };
            txtChatMessage.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SendChatMessage(); } };

            btnSendChat = new ModernButton { Text = "Enviar", Location = new Point(223, 400), Size = new Size(65, 32), NormalColor = Color.FromArgb(0, 172, 193), HoverColor = Color.FromArgb(0, 131, 143), BorderRadius = 6 };
            btnSendChat.Click += (s, e) => { SendChatMessage(); };

            panelChatDrawer.Controls.Add(lblChatHeader);
            panelChatDrawer.Controls.Add(btnCloseChatDrawer);
            panelChatDrawer.Controls.Add(txtChatHistory);
            panelChatDrawer.Controls.Add(txtChatMessage);
            panelChatDrawer.Controls.Add(btnSendChat);

            btnChat.Click += (s, e) =>
            {
                if (!panelChatDrawer.Visible)
                {
                    panelChatDrawer.Location = new Point(this.ClientSize.Width - 320, topToolbar.Height + 10);
                    panelChatDrawer.BringToFront();
                    panelChatDrawer.Visible = true;
                    txtChatMessage.Focus();
                }
                else
                {
                    panelChatDrawer.Visible = false;
                    this.ActiveControl = picRemoteDesktop;
                }
            };

            overlayReconnecting = new Panel
            {
                Size = new Size(420, 60),
                BackColor = Color.FromArgb(239, 68, 68),
                Visible = false
            };
            lblReconnectingText = new Label
            {
                Text = "Reconectando sesión en tiempo real...\nRestableciendo enlace con el host remoto.",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            overlayReconnecting.Controls.Add(lblReconnectingText);

            picRemoteDesktop = new SmoothPictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black
            };

            picRemoteDesktop.Controls.Add(overlayReconnecting);
            picRemoteDesktop.Resize += (s, e) =>
            {
                overlayReconnecting.Location = new Point((picRemoteDesktop.Width - overlayReconnecting.Width) / 2, 20);
            };

            picRemoteDesktop.MouseDown += PicRemoteDesktop_MouseDown;
            picRemoteDesktop.MouseMove += PicRemoteDesktop_MouseMove;
            picRemoteDesktop.MouseUp += PicRemoteDesktop_MouseUp;
            picRemoteDesktop.MouseWheel += PicRemoteDesktop_MouseWheel;
            picRemoteDesktop.Click += (s, e) => { this.ActiveControl = picRemoteDesktop; };

            this.Controls.Add(picRemoteDesktop);
            this.Controls.Add(panelChatDrawer);
            panelChatDrawer.BringToFront();
            this.Controls.Add(topToolbar);

            this.FormClosing += (s, e) => { CloseSession(); };
            this.Resize += (s, e) =>
            {
                if (panelChatDrawer.Visible)
                {
                    panelChatDrawer.Location = new Point(this.ClientSize.Width - 320, topToolbar.Height + 10);
                }
            };

            receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
            receiveThread.Start();

            StartClipboardSyncThread();
            InstallKeyboardHook();
        }

        private ContextMenuStrip menuView;
        private ContextMenuStrip menuMainMenu;

        private void BuildViewMenu(Button btnTarget)
        {
            menuView = new ContextMenuStrip();
            menuView.Items.Add("Modo Pantalla Completa", null, (s, e) =>
            {
                if (this.FormBorderStyle == FormBorderStyle.None)
                {
                    this.FormBorderStyle = FormBorderStyle.Sizable;
                    this.WindowState = FormWindowState.Normal;
                }
                else
                {
                    this.FormBorderStyle = FormBorderStyle.None;
                    this.WindowState = FormWindowState.Maximized;
                }
            });
            menuView.Items.Add("-");
            menuView.Items.Add("Proporción Adaptada (Zoom)", null, (s, e) => { picRemoteDesktop.SizeMode = PictureBoxSizeMode.Zoom; });
            menuView.Items.Add("Rellenar Ventana (Stretch)", null, (s, e) => { picRemoteDesktop.SizeMode = PictureBoxSizeMode.StretchImage; });
            btnTarget.Click += (s, e) => { menuView.Show(btnTarget, new Point(0, btnTarget.Height)); };
        }

        private void BuildMainMenu(Button btnTarget)
        {
            menuMainMenu = new ContextMenuStrip();
            menuMainMenu.Items.Add("Sobre Connecting...", null, (s, e) =>
            {
                using (AboutForm about = new AboutForm())
                {
                    about.ShowDialog();
                }
            });
            menuMainMenu.Items.Add("Ayuda y Documentación", null, (s, e) =>
            {
                try { Process.Start("https://jh4n3r.github.io/connecting/docs/"); } catch { }
            });
            menuMainMenu.Items.Add("-");
            menuMainMenu.Items.Add("Finalizar Sesión Remota", null, (s, e) => { CloseSession(); });
            btnTarget.Click += (s, e) => { menuMainMenu.Show(btnTarget, new Point(0, btnTarget.Height)); };
        }

        private void BuildQuickActionsMenu()
        {
            menuQuickActions = new ContextMenuStrip();
            menuQuickActions.Items.Add("Enviar Ctrl + Alt + Supr", null, (s, e) => { SendKeyCombo(VK_CONTROL, VK_MENU, VK_DELETE); });
            menuQuickActions.Items.Add("Administrador de Tareas", null, (s, e) => { SendKeyCombo(VK_CONTROL, 0x10, 0x1B); });
            menuQuickActions.Items.Add("Mostrar Escritorio", null, (s, e) => { SendKeyCombo(VK_LWIN, (byte)'D'); });
            menuQuickActions.Items.Add("Explorador de Archivos", null, (s, e) => { SendKeyCombo(VK_LWIN, (byte)'E'); });
            menuQuickActions.Items.Add("Bloquear Equipo Remoto", null, (s, e) => { SendKeyCombo(VK_LWIN, (byte)'L'); });
            menuQuickActions.Items.Add("-");
            menuQuickActions.Items.Add("Capturar Pantalla a PNG", null, (s, e) =>
            {
                if (picRemoteDesktop.Image != null)
                {
                    try
                    {
                        string picturesDir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                        string connectingDir = Path.Combine(picturesDir, "Connecting");
                        if (!Directory.Exists(connectingDir))
                        {
                            Directory.CreateDirectory(connectingDir);
                        }

                        string path = Path.Combine(connectingDir, string.Format("Captura_Connecting_{0}_{1}.png", remoteTargetId, DateTime.Now.ToString("yyyyMMdd_HHmmss")));
                        picRemoteDesktop.Image.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                        MessageBox.Show("Captura de pantalla guardada en Imágenes\\Connecting:\n" + path, "Captura Guardada", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch { }
                }
            });
        }

        private Image _pendingFrame = null;
        private bool _isRenderingFrame = false;

        private void OnFrameReceived(byte[] payload)
        {
            try
            {
                byte[] copy = new byte[payload.Length];
                Buffer.BlockCopy(payload, 0, copy, 0, payload.Length);
                MemoryStream ms = new MemoryStream(copy);
                Image newImg = Image.FromStream(ms, false, false);
                
                Image oldPending = null;
                lock (this)
                {
                    oldPending = _pendingFrame;
                    _pendingFrame = newImg;
                }
                if (oldPending != null) oldPending.Dispose();

                if (!_isRenderingFrame)
                {
                    _isRenderingFrame = true;
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        Image toRender = null;
                        lock (this)
                        {
                            toRender = _pendingFrame;
                            _pendingFrame = null;
                        }

                        if (toRender != null)
                        {
                            Image old = picRemoteDesktop.Image;
                            picRemoteDesktop.Image = toRender;
                            if (old != null) old.Dispose();

                            _fpsFrameCount++;
                            long now = Environment.TickCount;
                            if (now - _lastFpsUpdate >= 1000)
                            {
                                int fps = _fpsFrameCount;
                                _fpsFrameCount = 0;
                                _lastFpsUpdate = now;
                                if (lblFps != null && !lblFps.IsDisposed)
                                {
                                    int estimatedMs = Math.Max(8, 1000 / Math.Max(1, fps));
                                    lblFps.Text = string.Format("Latencia: ~{0} ms", estimatedMs);
                                }
                            }
                        }
                        _isRenderingFrame = false;
                    });
                }
            }
            catch { }
        }

        private PointF GetNormalizedCoords(int mouseX, int mouseY)
        {
            int boxW = picRemoteDesktop.Width;
            int boxH = picRemoteDesktop.Height;
            if (boxW <= 0 || boxH <= 0) return PointF.Empty;

            Image img = picRemoteDesktop.Image;
            if (img != null && picRemoteDesktop.SizeMode == PictureBoxSizeMode.Zoom)
            {
                int imgW = img.Width;
                int imgH = img.Height;
                if (imgW > 0 && imgH > 0)
                {
                    float ratioImg = (float)imgW / imgH;
                    float ratioBox = (float)boxW / boxH;

                    float drawW = boxW;
                    float drawH = boxH;
                    float offsetX = 0;
                    float offsetY = 0;

                    if (ratioImg > ratioBox)
                    {
                        drawH = boxW / ratioImg;
                        offsetY = (boxH - drawH) / 2f;
                    }
                    else
                    {
                        drawW = boxH * ratioImg;
                        offsetX = (boxW - drawW) / 2f;
                    }

                    float relX = mouseX - offsetX;
                    float relY = mouseY - offsetY;

                    if (relX >= 0 && relX <= drawW && relY >= 0 && relY <= drawH && drawW > 0 && drawH > 0)
                    {
                        float normX = Math.Max(0f, Math.Min(1f, relX / drawW));
                        float normY = Math.Max(0f, Math.Min(1f, relY / drawH));
                        return new PointF(normX, normY);
                    }
                }
            }

            float nX = Math.Max(0f, Math.Min(1f, (float)mouseX / boxW));
            float nY = Math.Max(0f, Math.Min(1f, (float)mouseY / boxH));
            return new PointF(nX, nY);
        }

        private long _lastMouseMoveTick = 0;

        private void PicRemoteDesktop_MouseDown(object sender, MouseEventArgs e)
        {
            this.ActiveControl = picRemoteDesktop;
            byte evtType = (e.Button == MouseButtons.Right) ? (byte)0x04 : (byte)0x02;
            SendRemoteInput(e.X, e.Y, evtType);
        }

        private void PicRemoteDesktop_MouseMove(object sender, MouseEventArgs e)
        {
            long now = Environment.TickCount;
            if (now - _lastMouseMoveTick < 3) return;
            _lastMouseMoveTick = now;
            SendRemoteInput(e.X, e.Y, 0x01);
        }

        private void PicRemoteDesktop_MouseUp(object sender, MouseEventArgs e)
        {
            byte evtType = (e.Button == MouseButtons.Right) ? (byte)0x05 : (byte)0x03;
            SendRemoteInput(e.X, e.Y, evtType);
        }

        private void PicRemoteDesktop_MouseWheel(object sender, MouseEventArgs e)
        {
            byte evtType = (e.Delta > 0) ? (byte)0x06 : (byte)0x07;
            SendRemoteInput(e.X, e.Y, evtType);
        }

        private void SendRemoteInput(int mouseX, int mouseY, byte evtType)
        {
            try
            {
                PointF norm = GetNormalizedCoords(mouseX, mouseY);
                if (norm.IsEmpty) return;

                byte[] data = new byte[9];
                data[0] = evtType;
                BitConverter.GetBytes(norm.X).CopyTo(data, 1);
                BitConverter.GetBytes(norm.Y).CopyTo(data, 5);

                PacketProtocol.SendPacket(stream, 0x01, data);
            }
            catch { }
        }

        private void SendKeyCombo(params byte[] keys)
        {
            Thread t = new Thread(() =>
            {
                try
                {
                    for (int i = 0; i < keys.Length; i++)
                    {
                        SendKeyboardInput(keys[i], true);
                        Thread.Sleep(50);
                    }
                    Thread.Sleep(100);
                    for (int i = keys.Length - 1; i >= 0; i--)
                    {
                        SendKeyboardInput(keys[i], false);
                        Thread.Sleep(50);
                    }
                }
                catch { }
            }) { IsBackground = true };
            t.Start();
        }

        private void InstallKeyboardHook()
        {
            _keyboardProc = HookCallback;
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                _keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && GetForegroundWindow() == this.Handle && !txtChatMessage.Focused)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                bool isDown = (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN);
                bool isUp = (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP);

                if (isDown || isUp)
                {
                    SendKeyboardInput((byte)vkCode, isDown);
                    return (IntPtr)1;
                }
            }
            return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
        }

        private void StartClipboardSyncThread()
        {
            clipboardThread = new Thread(() =>
            {
                while (isSessionActive && client != null && client.Connected)
                {
                    try
                    {
                        string currentText = "";
                        this.Invoke((MethodInvoker)delegate
                        {
                            if (Clipboard.ContainsText()) currentText = Clipboard.GetText();
                        });

                        if (!string.IsNullOrEmpty(currentText) && currentText != lastClipboardText)
                        {
                            lastClipboardText = currentText;
                            byte[] clipBytes = Encoding.UTF8.GetBytes(currentText);
                            PacketProtocol.SendPacket(stream, 0x04, clipBytes);
                        }
                    }
                    catch { }
                    Thread.Sleep(400);
                }
            }) { IsBackground = true };
            clipboardThread.SetApartmentState(ApartmentState.STA);
            clipboardThread.Start();
        }

        private void SendChatMessage()
        {
            string msg = txtChatMessage.Text.Trim();
            if (string.IsNullOrEmpty(msg)) return;

            string myName = PeerResolver.GetUserDisplayName();
            txtChatHistory.AppendText(myName + " (Yo): " + msg + "\n");
            txtChatHistory.ScrollToCaret();
            txtChatMessage.Clear();

            byte[] msgBytes = Encoding.UTF8.GetBytes(myName + ": " + msg);
            PacketProtocol.SendPacket(stream, 0x03, msgBytes);
        }

        private void ReceiveLoop()
        {
            int errorCount = 0;
            while (isSessionActive)
            {
                try
                {
                    if (client == null || !client.Connected)
                    {
                        errorCount++;
                        if (errorCount > 3)
                        {
                            NotifyHostClosed();
                            break;
                        }
                        ShowReconnectingOverlay(true);
                        Thread.Sleep(1000);
                        continue;
                    }

                    byte pktType;
                    byte[] payload;
                    if (!PacketProtocol.ReadPacket(stream, out pktType, out payload))
                    {
                        errorCount++;
                        if (errorCount > 3)
                        {
                            NotifyHostClosed();
                            break;
                        }
                        ShowReconnectingOverlay(true);
                        Thread.Sleep(1000);
                        continue;
                    }

                    errorCount = 0;
                    ShowReconnectingOverlay(false);

                    if (pktType == 0x03) // CHAT
                    {
                        string chatStr = Encoding.UTF8.GetString(payload);
                        this.Invoke((MethodInvoker)delegate
                        {
                            panelChatDrawer.Location = new Point(this.ClientSize.Width - 320, topToolbar.Height + 10);
                            panelChatDrawer.BringToFront();
                            panelChatDrawer.Visible = true;
                            txtChatHistory.AppendText(chatStr + "\n");
                            txtChatHistory.ScrollToCaret();
                        });
                    }
                    else if (pktType == 0x04) // CLIPBOARD
                    {
                        string clipText = Encoding.UTF8.GetString(payload);
                        this.Invoke((MethodInvoker)delegate
                        {
                            try
                            {
                                lastClipboardText = clipText;
                                Clipboard.SetText(clipText);
                            }
                            catch { }
                        });
                    }
                    else if (pktType == 0x00) // PANTALLA
                    {
                        OnFrameReceived(payload);
                    }
                }
                catch
                {
                    errorCount++;
                    if (errorCount > 3)
                    {
                        NotifyHostClosed();
                        break;
                    }
                    ShowReconnectingOverlay(true);
                    Thread.Sleep(1000);
                }
            }
        }

        private void NotifyHostClosed()
        {
            if (!isSessionActive) return;
            isSessionActive = false;
            this.Invoke((MethodInvoker)delegate
            {
                MessageBox.Show("La sesión remota ha finalizado porque el equipo remoto cerró la aplicación o se desconectó de la red.", "Sesión Finalizada", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            });
        }

        private void ShowReconnectingOverlay(bool visible)
        {
            try
            {
                this.Invoke((MethodInvoker)delegate
                {
                    overlayReconnecting.Visible = visible;
                    overlayReconnecting.Location = new Point((picRemoteDesktop.Width - overlayReconnecting.Width) / 2, 20);
                });
            }
            catch { }
        }



        private void SendKeyboardInput(byte keyCode, bool isDown)
        {
            try
            {
                byte[] data = new byte[2];
                data[0] = keyCode;
                data[1] = isDown ? (byte)0x01 : (byte)0x00;

                PacketProtocol.SendPacket(stream, 0x02, data);
            }
            catch { }
        }

        private void CloseSession()
        {
            isSessionActive = false;
            if (_keyboardHookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_keyboardHookId);
                _keyboardHookId = IntPtr.Zero;
            }
            try
            {
                if (stream != null)
                {
                    byte[] discPayload = Encoding.UTF8.GetBytes("DISCONNECT");
                    PacketProtocol.SendPacket(stream, 0xFF, discPayload);
                }
            }
            catch { }
            try { if (client != null) client.Close(); } catch { }
            this.Close();
        }
    }

    public class AboutForm : Form
    {
        public AboutForm()
        {
            this.Text = "Sobre Connecting Remote Desktop";
            this.Size = new Size(460, 310);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.White;

            Label lblAppName = new Label { Text = "Connecting Remote Desktop", Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = Color.FromArgb(0, 131, 143), Location = new Point(24, 20), AutoSize = true };
            Label lblVersion = new Label { Text = "Versión: 1.0.1 Portátil (Enterprise)", Font = new Font("Segoe UI", 9.5F, FontStyle.Regular), ForeColor = Color.FromArgb(100, 116, 139), Location = new Point(24, 52), AutoSize = true };
            Label lblDesc = new Label { Text = "Plataforma de asistencia y control remoto portátil cifrada sin instalación.\n\n• Transmisión nativa de alta definición (100% resolución nativa)\n• Medidor de cuadros por segundo (FPS) en tiempo real\n• Cifrado de datos y canal seguro On-Premise\n• Soporte nativo para elevación de permisos (UAC)", Font = new Font("Segoe UI", 9F), Location = new Point(24, 88), Size = new Size(400, 110) };

            Button btnOk = new Button { Text = "Aceptar", Location = new Point(310, 215), Size = new Size(110, 34), BackColor = Color.FromArgb(0, 172, 193), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.OK, Cursor = Cursors.Hand };
            this.Controls.Add(lblAppName);
            this.Controls.Add(lblVersion);
            this.Controls.Add(lblDesc);
            this.Controls.Add(btnOk);
            this.AcceptButton = btnOk;
        }
    }

    // =========================================================================
    // 4. MAIN LAUNCHER DASHBOARD CON HISTORIAL DE SESIONES Y ALIAS
    // =========================================================================
    public class MainForm : Form
    {
        private readonly Color ColorBg = Color.FromArgb(248, 250, 252);
        private readonly Color ColorCardBg = Color.White;
        private readonly Color ColorCyanPrimary = Color.FromArgb(0, 172, 193);
        private readonly Color ColorCyanDark = Color.FromArgb(0, 131, 143);
        private readonly Color ColorTextDark = Color.FromArgb(15, 23, 42);
        private readonly Color ColorTextMuted = Color.FromArgb(100, 116, 139);

        private Label lblTitle;
        private Label lblSubtitle;
        private Label lblStatus;
        private Label lblMyToken;
        private Label lblPskToken;
        private TextBox txtRemoteId;
        private TextBox txtRemotePsk;
        private TextBox txtCustomPsk;
        private CheckBox chkUnattendedAccess;
        private ModernButton btnConnect;
        private ModernButton btnCopyId;
        private ModernButton btnRegenerateId;

        private Panel panelNavHeader;
        private ModernButton btnNavDashboard;
        private ModernButton btnNavSettings;
        private Panel panelContentDashboard;
        private Panel panelContentSettings;
        private ModernCardPanel cardHistory;
        private FlowLayoutPanel flowHistory;

        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;

        private string myCcId;
        private string rawNumId;
        private string myPskToken;
        private int myBoundPort = 9000;
        private TcpListener tcpListener;
        private bool isHostRunning = true;
        private bool allowExit = false;
        private Thread serverThread;
        private Thread relayRegistrationThread;
        private TcpClient currentHostRelayClient;
        private HostSessionFloatingWidget currentFloatingWidget;

        public MainForm()
        {
            InitializeComponent();
            SetupSystemTray();
            GenerateMyCredentials(false);
            StartP2PServer();
            PeerResolver.RegisterLocalNode(rawNumId, myBoundPort, myPskToken);
            StartRelayHostRegistration();
        }

        private void SetupSystemTray()
        {
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Abrir Connecting", null, (s, e) => { RestoreFromTray(); });
            trayMenu.Items.Add(string.Format("ID Puesto: {0}", myCcId), null, (s, e) => { Clipboard.SetText(rawNumId); });
            trayMenu.Items.Add("-");
            trayMenu.Items.Add("Salir", null, (s, e) => { allowExit = true; Application.Exit(); });

            trayIcon = new NotifyIcon
            {
                Text = "Connecting Remote Desktop",
                Visible = true,
                ContextMenuStrip = trayMenu
            };
            try { trayIcon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            trayIcon.DoubleClick += (s, e) => { RestoreFromTray(); };

            this.FormClosing += (s, e) =>
            {
                if (!allowExit && e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    this.Hide();
                    trayIcon.ShowBalloonTip(2000, "Connecting", "La aplicación sigue activa en segundo plano para recibir solicitudes de asistencia.", ToolTipIcon.Info);
                }
            };
        }

        private void RestoreFromTray()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
        }

        private void GenerateMyCredentials(bool forceRegenerate)
        {
            if (forceRegenerate)
            {
                rawNumId = PeerResolver.GenerateRandom9DigitId();
                PeerResolver.SavePersistentId(rawNumId);

                try
                {
                    if (currentHostRelayClient != null)
                    {
                        currentHostRelayClient.Close();
                        currentHostRelayClient = null;
                    }
                }
                catch { }
            }
            else
            {
                rawNumId = PeerResolver.GetPersistentId();
            }

            myCcId = string.Format("{0} {1} {2}", rawNumId.Substring(0, 3), rawNumId.Substring(3, 3), rawNumId.Substring(6, 3));
            myPskToken = Math.Abs(Guid.NewGuid().GetHashCode()).ToString().Substring(0, 6);
            
            if (lblMyToken != null) lblMyToken.Text = myCcId;
            if (lblPskToken != null) lblPskToken.Text = "Clave PSK Segura: " + myPskToken;
        }

        private void StartRelayHostRegistration()
        {
            relayRegistrationThread = new Thread(() =>
            {
                while (isHostRunning)
                {
                    string idToRegister = rawNumId;
                    try
                    {
                        TcpClient relayClient = new TcpClient();
                        relayClient.NoDelay = true;
                        relayClient.SendBufferSize = 262144;
                        relayClient.ReceiveBufferSize = 262144;
                        currentHostRelayClient = relayClient;

                        string targetHost = PeerResolver.GetRelayServerHost();
                        int targetPort = PeerResolver.DefaultRelayPort;

                        IAsyncResult ar = relayClient.BeginConnect(targetHost, targetPort, null, null);
                        if (ar.AsyncWaitHandle.WaitOne(2000) && relayClient.Connected)
                        {
                            NetworkStream ns = relayClient.GetStream();
                            byte[] regBytes = Encoding.UTF8.GetBytes(string.Format("REGISTER:{0}\n", idToRegister));
                            ns.Write(regBytes, 0, regBytes.Length);
                            ns.Flush();

                            byte[] buf = new byte[256];
                            while (relayClient.Connected && isHostRunning && idToRegister == rawNumId)
                            {
                                int r = ns.Read(buf, 0, 256);
                                if (r <= 0) break;
                                string msg = Encoding.UTF8.GetString(buf, 0, r).Trim();

                                if (msg.StartsWith("INCOMING:"))
                                {
                                    string requestingId = msg.Split(':')[1].Trim();
                                    bool accepted = false;

                                    bool isUnattended = false;
                                    this.Invoke((MethodInvoker)delegate
                                    {
                                        isUnattended = chkUnattendedAccess != null && chkUnattendedAccess.Checked;
                                    });

                                    if (isUnattended)
                                    {
                                        accepted = true;
                                    }
                                    else
                                    {
                                        this.Invoke((MethodInvoker)delegate
                                        {
                                            using (ConnectionRequestForm reqForm = new ConnectionRequestForm(requestingId))
                                            {
                                                accepted = (reqForm.ShowDialog() == DialogResult.OK && reqForm.IsAccepted);
                                            }
                                        });
                                    }

                                    if (accepted)
                                    {
                                        string myMachineName = Environment.MachineName;
                                        byte[] okBytes = Encoding.UTF8.GetBytes(string.Format("ACCEPT_OK:{0}\n", myMachineName));
                                        ns.Write(okBytes, 0, okBytes.Length);
                                        ns.Flush();

                                        TcpClient activeRelayClient = relayClient;
                                        NetworkStream activeStream = ns;

                                        this.Invoke((MethodInvoker)delegate
                                        {
                                            if (currentFloatingWidget != null && !currentFloatingWidget.IsDisposed) currentFloatingWidget.Close();
                                            currentFloatingWidget = new HostSessionFloatingWidget(requestingId, activeStream, () =>
                                            {
                                                try { activeRelayClient.Close(); } catch { }
                                            });
                                            currentFloatingWidget.Show();
                                        });

                                        Thread inputReadThread = new Thread(() =>
                                        {
                                            while (activeRelayClient.Connected && isHostRunning)
                                            {
                                                try
                                                {
                                                    byte pktType;
                                                    byte[] payload;
                                                    if (!PacketProtocol.ReadPacket(activeStream, out pktType, out payload)) break;

                                                    if (pktType == 0xFF)
                                                    {
                                                        break;
                                                    }
                                                    else if (pktType == 0x01 && payload.Length >= 9)
                                                    {
                                                        byte evtType = payload[0];
                                                        float normX = BitConverter.ToSingle(payload, 1);
                                                        float normY = BitConverter.ToSingle(payload, 5);

                                                        NativeInputInjector.ExecuteMouseInput(evtType, normX, normY);
                                                    }
                                                    else if (pktType == 0x02 && payload.Length >= 2)
                                                    {
                                                        byte keyCode = payload[0];
                                                        bool isDown = payload[1] == 0x01;

                                                        NativeInputInjector.ExecuteKeyboardInput(keyCode, isDown);
                                                    }
                                                    else if (pktType == 0x03)
                                                    {
                                                        string chatMsg = Encoding.UTF8.GetString(payload);
                                                        if (chatMsg.StartsWith("CLIENT_DISCONNECTED")) break;
                                                        if (currentFloatingWidget != null && !currentFloatingWidget.IsDisposed)
                                                        {
                                                            currentFloatingWidget.AppendChatMessage("Cliente Remoto", chatMsg);
                                                        }
                                                    }
                                                    else if (pktType == 0x04)
                                                    {
                                                        string clipText = Encoding.UTF8.GetString(payload);
                                                        this.Invoke((MethodInvoker)delegate
                                                        {
                                                            try { Clipboard.SetText(clipText); } catch { }
                                                        });
                                                    }
                                                }
                                                catch { break; }
                                            }

                                            this.Invoke((MethodInvoker)delegate
                                            {
                                                if (currentFloatingWidget != null && !currentFloatingWidget.IsDisposed) currentFloatingWidget.Close();
                                            });
                                            try { activeRelayClient.Close(); } catch { }
                                        }) { IsBackground = true };
                                        inputReadThread.Start();

                                        while (activeRelayClient.Connected && isHostRunning)
                                        {
                                            byte[] rawFrame = DesktopCapturer.CaptureHighQualityJpeg();
                                            if (rawFrame != null && rawFrame.Length > 0)
                                            {
                                                if (!PacketProtocol.SendPacket(activeStream, 0x00, rawFrame)) break;
                                                Thread.Sleep(1);
                                            }
                                            else
                                            {
                                                Thread.Sleep(15);
                                            }
                                        }

                                        this.Invoke((MethodInvoker)delegate
                                        {
                                            if (currentFloatingWidget != null && !currentFloatingWidget.IsDisposed) currentFloatingWidget.Close();
                                        });
                                    }
                                    else
                                    {
                                        byte[] rejBytes = Encoding.UTF8.GetBytes("REJECTED\n");
                                        try { ns.Write(rejBytes, 0, rejBytes.Length); ns.Flush(); } catch { }
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                    finally
                    {
                        try { if (currentHostRelayClient != null) { currentHostRelayClient.Close(); currentHostRelayClient = null; } } catch { }
                    }

                    Thread.Sleep(1000);
                }
            }) { IsBackground = true };

            relayRegistrationThread.Start();
        }

        private void InitializeComponent()
        {
            this.Text = "Connecting - Solución de Escritorio Remoto";
            this.Size = new Size(1000, 750);
            this.MinimumSize = new Size(950, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = ColorBg;
            this.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);

            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Panel topHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 90,
                BackColor = Color.White,
                Padding = new Padding(24, 16, 24, 16)
            };

            PictureBox picLogo = new PictureBox
            {
                Size = new Size(48, 48),
                Location = new Point(24, 21),
                BackColor = Color.Transparent
            };
            Bitmap logoBmp = new Bitmap(48, 48);
            using (Graphics g = Graphics.FromImage(logoBmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.White);
                using (Pen penCircle = new Pen(ColorCyanPrimary, 3.5f)) g.DrawEllipse(penCircle, 2, 2, 43, 43);
                using (SolidBrush brushFill = new SolidBrush(Color.FromArgb(224, 247, 250))) g.FillEllipse(brushFill, 5, 5, 37, 37);
                using (SolidBrush brushScreenFront = new SolidBrush(ColorCyanPrimary)) g.FillRectangle(brushScreenFront, 13, 14, 22, 15);
                using (Pen penBorder = new Pen(Color.White, 1.5f)) g.DrawRectangle(penBorder, 13, 14, 22, 15);
                using (SolidBrush brushScreenFront = new SolidBrush(ColorCyanPrimary))
                {
                    g.FillRectangle(brushScreenFront, 21, 29, 6, 4);
                    g.FillRectangle(brushScreenFront, 17, 33, 14, 3);
                }
            }
            picLogo.Image = logoBmp;

            lblTitle = new Label
            {
                Text = "Connecting",
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                ForeColor = ColorTextDark,
                Location = new Point(84, 18),
                AutoSize = true
            };

            lblSubtitle = new Label
            {
                Text = "Plataforma de Control Remoto Portátil y Segura",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = ColorTextMuted,
                Location = new Point(87, 52),
                AutoSize = true
            };

            topHeader.Controls.Add(picLogo);
            topHeader.Controls.Add(lblTitle);
            topHeader.Controls.Add(lblSubtitle);

            panelNavHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.White,
                Padding = new Padding(24, 4, 24, 4)
            };

            Panel navDivider = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = Color.FromArgb(226, 232, 240)
            };
            panelNavHeader.Controls.Add(navDivider);

            btnNavDashboard = new ModernButton
            {
                Text = "Puesto de Trabajo & Conexión",
                Location = new Point(24, 6),
                Size = new Size(240, 36),
                NormalColor = ColorCyanPrimary,
                HoverColor = ColorCyanDark,
                BorderRadius = 6
            };
            btnNavDashboard.Click += (s, e) => SwitchTab(true);

            btnNavSettings = new ModernButton
            {
                Text = "Configuración & Seguridad",
                Location = new Point(274, 6),
                Size = new Size(240, 36),
                NormalColor = Color.FromArgb(241, 245, 249),
                HoverColor = Color.FromArgb(226, 232, 240),
                ForeColor = ColorTextDark,
                BorderRadius = 6
            };
            btnNavSettings.Click += (s, e) => SwitchTab(false);

            panelNavHeader.Controls.Add(btnNavDashboard);
            panelNavHeader.Controls.Add(btnNavSettings);

            panelContentDashboard = new Panel { Dock = DockStyle.Fill, BackColor = ColorBg, Padding = new Padding(24), AutoScroll = true };
            panelContentSettings = new Panel { Dock = DockStyle.Fill, BackColor = ColorBg, Padding = new Padding(24), Visible = false };

            BuildDashboardTab();
            BuildSettingsTab();

            this.Controls.Add(panelContentDashboard);
            this.Controls.Add(panelContentSettings);
            this.Controls.Add(panelNavHeader);
            this.Controls.Add(topHeader);
        }

        private void SwitchTab(bool showDashboard)
        {
            panelContentDashboard.Visible = showDashboard;
            panelContentSettings.Visible = !showDashboard;

            if (showDashboard)
            {
                btnNavDashboard.NormalColor = ColorCyanPrimary;
                btnNavDashboard.ForeColor = Color.White;

                btnNavSettings.NormalColor = Color.FromArgb(241, 245, 249);
                btnNavSettings.ForeColor = ColorTextDark;
                RefreshHistoryGrid();
            }
            else
            {
                btnNavSettings.NormalColor = ColorCyanPrimary;
                btnNavSettings.ForeColor = Color.White;

                btnNavDashboard.NormalColor = Color.FromArgb(241, 245, 249);
                btnNavDashboard.ForeColor = ColorTextDark;
            }
        }

        private void BuildDashboardTab()
        {
            ModernCardPanel cardLocalToken = new ModernCardPanel
            {
                Size = new Size(930, 160),
                Location = new Point(24, 20),
                BackColor = ColorCardBg,
                BorderRadius = 12
            };

            lblStatus = new Label
            {
                Text = "RED CONNECTING EN LÍNEA (0 MS LATENCIA)",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(16, 185, 129),
                Location = new Point(24, 16),
                AutoSize = true
            };

            Label lblMyTokenTitle = new Label
            {
                Text = "Tu ID de Acceso Permanente (Este Puesto):",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = ColorTextDark,
                Location = new Point(24, 40),
                AutoSize = true
            };

            lblMyToken = new Label
            {
                Text = myCcId,
                Font = new Font("Segoe UI", 32F, FontStyle.Bold),
                ForeColor = ColorCyanDark,
                Location = new Point(20, 62),
                AutoSize = true
            };

            lblPskToken = new Label
            {
                Text = "Clave PSK Segura: " + myPskToken,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(225, 29, 72),
                Location = new Point(24, 124),
                AutoSize = true
            };

            btnCopyId = new ModernButton
            {
                Text = "Copiar ID",
                Location = new Point(480, 70),
                Size = new Size(130, 42),
                NormalColor = Color.FromArgb(241, 245, 249),
                HoverColor = Color.FromArgb(226, 232, 240),
                ForeColor = ColorTextDark,
                BorderRadius = 8
            };
            btnCopyId.Click += (s, e) => { try { Clipboard.SetText(rawNumId); } catch { } };

            btnRegenerateId = new ModernButton
            {
                Text = "Regenerar ID",
                Location = new Point(620, 70),
                Size = new Size(140, 42),
                NormalColor = Color.FromArgb(241, 245, 249),
                HoverColor = Color.FromArgb(226, 232, 240),
                ForeColor = ColorTextDark,
                BorderRadius = 8
            };
            btnRegenerateId.Click += (s, e) =>
            {
                if (MessageBox.Show("¿Desea generar una nueva ID permanente para este equipo?", "Regenerar ID", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    GenerateMyCredentials(true);
                    MessageBox.Show("Nueva ID Permanente asignada: " + myCcId, "Connecting", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            cardLocalToken.Controls.Add(lblStatus);
            cardLocalToken.Controls.Add(lblMyTokenTitle);
            cardLocalToken.Controls.Add(lblMyToken);
            cardLocalToken.Controls.Add(lblPskToken);
            cardLocalToken.Controls.Add(btnCopyId);
            cardLocalToken.Controls.Add(btnRegenerateId);

            ModernCardPanel cardRemoteConnect = new ModernCardPanel
            {
                Size = new Size(930, 180),
                Location = new Point(24, 195),
                BackColor = ColorCardBg,
                BorderRadius = 12
            };

            Label lblRemoteTitle = new Label
            {
                Text = "Conectar a un Escritorio Remoto:",
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = ColorTextDark,
                Location = new Point(24, 16),
                AutoSize = true
            };

            ModernInputContainer inputContainerId = new ModernInputContainer
            {
                Location = new Point(24, 46),
                Size = new Size(590, 48),
                BorderRadius = 8
            };

            txtRemoteId = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 14F),
                ForeColor = ColorTextDark,
                BorderStyle = BorderStyle.None,
                Text = ""
            };
            // AUTO-AVANCE A PSK AL PRESIONAR ENTER EN ID
            txtRemoteId.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    txtRemotePsk.Focus();
                }
            };
            inputContainerId.Controls.Add(txtRemoteId);

            Label lblPskLabel = new Label
            {
                Text = "Clave PSK (Obligatoria):",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = ColorTextDark,
                Location = new Point(24, 114),
                AutoSize = true
            };

            ModernInputContainer inputContainerPsk = new ModernInputContainer
            {
                Location = new Point(210, 106),
                Size = new Size(220, 42),
                BorderRadius = 8
            };

            txtRemotePsk = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 12F),
                ForeColor = ColorTextDark,
                BorderStyle = BorderStyle.None,
                UseSystemPasswordChar = true,
                Text = ""
            };
            // CONEXIÓN AUTOMÁTICA AL PRESIONAR ENTER EN PSK
            txtRemotePsk.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    BtnConnect_Click(btnConnect, EventArgs.Empty);
                }
            };
            inputContainerPsk.Controls.Add(txtRemotePsk);

            btnConnect = new ModernButton
            {
                Text = "CONECTAR EN TIEMPO REAL",
                Location = new Point(635, 46),
                Size = new Size(265, 48),
                NormalColor = ColorCyanPrimary,
                HoverColor = ColorCyanDark,
                BorderRadius = 8
            };
            btnConnect.Click += BtnConnect_Click;

            cardRemoteConnect.Controls.Add(lblRemoteTitle);
            cardRemoteConnect.Controls.Add(inputContainerId);
            cardRemoteConnect.Controls.Add(lblPskLabel);
            cardRemoteConnect.Controls.Add(inputContainerPsk);
            cardRemoteConnect.Controls.Add(btnConnect);

            cardHistory = new ModernCardPanel
            {
                Size = new Size(930, 200),
                Location = new Point(24, 390),
                BackColor = ColorCardBg,
                BorderRadius = 12
            };

            Label lblHistHeader = new Label
            {
                Text = "PUESTOS DE TRABAJO RECIENTES (HISTORIAL DE SESIONES)",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = ColorTextDark,
                Location = new Point(24, 14),
                AutoSize = true
            };

            Button btnClearHist = new Button
            {
                Text = "Limpiar Todo",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(225, 29, 72),
                BackColor = Color.FromArgb(254, 242, 242),
                Location = new Point(780, 10),
                Size = new Size(110, 26),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnClearHist.FlatAppearance.BorderSize = 1;
            btnClearHist.FlatAppearance.BorderColor = Color.FromArgb(254, 202, 202);
            btnClearHist.Click += (s, e) =>
            {
                if (MessageBox.Show("¿Desea borrar todo el historial de conexiones recientes?", "Limpiar Historial", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    ConnectionHistoryManager.ClearAll();
                    RefreshHistoryGrid();
                }
            };

            flowHistory = new FlowLayoutPanel
            {
                Location = new Point(24, 42),
                Size = new Size(880, 145),
                AutoScroll = true,
                WrapContents = false
            };

            cardHistory.Controls.Add(lblHistHeader);
            cardHistory.Controls.Add(btnClearHist);
            cardHistory.Controls.Add(flowHistory);

            panelContentDashboard.Controls.Add(cardLocalToken);
            panelContentDashboard.Controls.Add(cardRemoteConnect);
            panelContentDashboard.Controls.Add(cardHistory);

            RefreshHistoryGrid();
        }

        private void RefreshHistoryGrid()
        {
            if (flowHistory == null) return;
            flowHistory.Controls.Clear();

            List<HistoryItem> recentItems = ConnectionHistoryManager.GetRecentSessions();
            if (recentItems.Count == 0)
            {
                Label lblEmpty = new Label
                {
                    Text = "No hay conexiones recientes aún. Los escritorios a los que te conectes aparecerán aquí.",
                    Font = new Font("Segoe UI", 9.5F, FontStyle.Italic),
                    ForeColor = ColorTextMuted,
                    AutoSize = true,
                    Margin = new Padding(10, 20, 0, 0)
                };
                flowHistory.Controls.Add(lblEmpty);
                return;
            }

            foreach (HistoryItem item in recentItems)
            {
                Panel card = new Panel
                {
                    Size = new Size(210, 125),
                    BackColor = Color.FromArgb(248, 250, 252),
                    BorderStyle = BorderStyle.FixedSingle,
                    Margin = new Padding(0, 0, 16, 0)
                };

                string displayTitle = !string.IsNullOrEmpty(item.Alias) ? item.Alias : item.Hostname;

                Label lblAliasHost = new Label
                {
                    Text = displayTitle,
                    Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                    ForeColor = ColorTextDark,
                    Location = new Point(8, 8),
                    Size = new Size(135, 22),
                    AutoEllipsis = true
                };

                Label lblId = new Label
                {
                    Text = string.Format("ID: {0}", item.Id),
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                    ForeColor = ColorTextMuted,
                    Location = new Point(8, 30),
                    AutoSize = true
                };

                Button btnEditAlias = new Button
                {
                    Text = "✏️",
                    Location = new Point(148, 6),
                    Size = new Size(26, 24),
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand,
                    Font = new Font("Segoe UI", 7.5F)
                };
                btnEditAlias.FlatAppearance.BorderSize = 0;

                Button btnDelete = new Button
                {
                    Text = "✕",
                    Location = new Point(176, 6),
                    Size = new Size(26, 24),
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand,
                    Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(225, 29, 72)
                };
                btnDelete.FlatAppearance.BorderSize = 0;

                string currentTargetId = item.Id;
                string currentAlias = item.Alias;

                btnEditAlias.Click += (s, e) =>
                {
                    string input = PromptInput("Asignar Alias o Nombre Personalizado", string.Format("Introduzca un nombre o alias para el puesto ID ({0}):", currentTargetId), currentAlias);
                    if (input != null)
                    {
                        ConnectionHistoryManager.UpdateAlias(currentTargetId, input);
                        RefreshHistoryGrid();
                    }
                };

                btnDelete.Click += (s, e) =>
                {
                    if (MessageBox.Show(string.Format("¿Desea eliminar el puesto ID ({0}) del historial?", currentTargetId), "Eliminar Puesto", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        ConnectionHistoryManager.RemoveSession(currentTargetId);
                        RefreshHistoryGrid();
                    }
                };

                ModernButton btnQuickConn = new ModernButton
                {
                    Text = "Conectar",
                    Location = new Point(8, 70),
                    Size = new Size(192, 38),
                    NormalColor = ColorCyanPrimary,
                    HoverColor = ColorCyanDark,
                    BorderRadius = 6
                };
                btnQuickConn.Click += (s, e) =>
                {
                    txtRemoteId.Text = currentTargetId;
                    txtRemotePsk.Focus();
                };

                card.Controls.Add(lblAliasHost);
                card.Controls.Add(lblId);
                card.Controls.Add(btnEditAlias);
                card.Controls.Add(btnDelete);
                card.Controls.Add(btnQuickConn);
                flowHistory.Controls.Add(card);
            }
        }

        private string PromptInput(string title, string promptText, string defaultValue)
        {
            Form prompt = new Form()
            {
                Width = 420,
                Height = 190,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = title,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.White
            };
            Label textLabel = new Label() { Left = 20, Top = 16, Text = promptText, AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            TextBox textBox = new TextBox() { Left = 20, Top = 50, Width = 360, Font = new Font("Segoe UI", 10F), Text = defaultValue };
            Button confirmation = new Button() { Text = "Guardar", Left = 260, Width = 120, Top = 95, DialogResult = DialogResult.OK, Height = 34, BackColor = ColorCyanPrimary, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(textLabel);
            prompt.AcceptButton = confirmation;

            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : null;
        }

        private void BuildSettingsTab()
        {
            ModernCardPanel cardSec = new ModernCardPanel { Size = new Size(930, 420), Location = new Point(24, 20), BackColor = ColorCardBg, BorderRadius = 12, Padding = new Padding(24) };
            Label lblSecHeader = new Label { Text = "Configuración Global de Seguridad y Acceso Desatendido", Font = new Font("Segoe UI", 13F, FontStyle.Bold), Location = new Point(24, 20), AutoSize = true, ForeColor = ColorTextDark };

            chkUnattendedAccess = new CheckBox { Text = "Permitir Acceso Desatendido directo con Clave PSK (sin confirmación de Aceptar)", Checked = true, Location = new Point(24, 65), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            
            Label lblCustomPskLabel = new Label { Text = "Clave de Acceso Desatendido Personalizada:", Location = new Point(24, 105), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            
            txtCustomPsk = new TextBox
            {
                Location = new Point(350, 102),
                Size = new Size(180, 28),
                Font = new Font("Segoe UI", 10.5F),
                UseSystemPasswordChar = true,
                Text = PeerResolver.GetCustomPsk()
            };
            // GUARDAR AUTOMÁTICAMENTE LA CLAVE PSK PERSONALIZADA AL EDITAR
            txtCustomPsk.TextChanged += (s, e) =>
            {
                PeerResolver.SaveCustomPsk(txtCustomPsk.Text);
            };

            CheckBox chkShowCustomPsk = new CheckBox
            {
                Text = "Mostrar Clave",
                Location = new Point(540, 105),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F)
            };
            chkShowCustomPsk.CheckedChanged += (s, e) =>
            {
                txtCustomPsk.UseSystemPasswordChar = !chkShowCustomPsk.Checked;
            };



            Label lblUserAliasLabel = new Label { Text = "Nombre de Presentación (Alias en Chat y Conexión):", Location = new Point(24, 150), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            TextBox txtUserAlias = new TextBox
            {
                Location = new Point(370, 147),
                Size = new Size(160, 28),
                Font = new Font("Segoe UI", 10F),
                Text = PeerResolver.GetUserDisplayName()
            };
            txtUserAlias.TextChanged += (s, e) =>
            {
                PeerResolver.SaveUserDisplayName(txtUserAlias.Text);
            };

            Label lblServerHostLabel = new Label { Text = "Servidor Relay Privado (IP / Dominio On-Premise):", Location = new Point(24, 190), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            TextBox txtServerHost = new TextBox
            {
                Location = new Point(370, 187),
                Size = new Size(220, 28),
                Font = new Font("Segoe UI", 10F),
                Text = PeerResolver.GetRelayServerHost()
            };
            txtServerHost.TextChanged += (s, e) =>
            {
                PeerResolver.SaveRelayServerHost(txtServerHost.Text);
            };

            CheckBox c2 = new CheckBox { Text = "Aislamiento total de teclado sin interferencias locales (ProcessDialogKey + Hook)", Checked = true, Location = new Point(24, 230), AutoSize = true, Font = new Font("Segoe UI", 10F) };
            CheckBox c3 = new CheckBox { Text = "Acceder a portapapeles bidireccional en tiempo real", Checked = true, Location = new Point(24, 265), AutoSize = true, Font = new Font("Segoe UI", 10F) };
            CheckBox c4 = new CheckBox { Text = "Minimizar a la barra de tareas (Segundo Plano al presionar Cerrar X)", Checked = true, Location = new Point(24, 300), AutoSize = true, Font = new Font("Segoe UI", 10F) };
            CheckBox cAudio = new CheckBox { Text = "Transmitir Audio del Equipo Remoto (Desactivado por defecto)", Checked = false, Location = new Point(24, 335), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.FromArgb(71, 85, 105) };

            cardSec.Controls.Add(lblSecHeader);
            cardSec.Controls.Add(chkUnattendedAccess);
            cardSec.Controls.Add(lblCustomPskLabel);
            cardSec.Controls.Add(txtCustomPsk);
            cardSec.Controls.Add(chkShowCustomPsk);
            cardSec.Controls.Add(lblUserAliasLabel);
            cardSec.Controls.Add(txtUserAlias);
            cardSec.Controls.Add(lblServerHostLabel);
            cardSec.Controls.Add(txtServerHost);
            cardSec.Controls.Add(c2);
            cardSec.Controls.Add(c3);
            cardSec.Controls.Add(c4);
            cardSec.Controls.Add(cAudio);

            panelContentSettings.Controls.Add(cardSec);
        }

        private void ListenForIncomingConnections()
        {
            for (int p = 9000; p <= 9050; p++)
            {
                try
                {
                    tcpListener = new TcpListener(IPAddress.Any, p);
                    tcpListener.Start();
                    myBoundPort = p;
                    break;
                }
                catch { }
            }

            while (isHostRunning)
            {
                try
                {
                    TcpClient incomingClient = tcpListener.AcceptTcpClient();
                    incomingClient.NoDelay = true;
                    NetworkStream stream = incomingClient.GetStream();

                    byte[] headerBuf = new byte[128];
                    int r = stream.Read(headerBuf, 0, 128);
                    if (r <= 0) { incomingClient.Close(); continue; }

                    string msg = Encoding.UTF8.GetString(headerBuf, 0, r).Trim();

                    if (msg.StartsWith("CONNECT:"))
                    {
                        string[] parts = msg.Split(':');
                        if (parts.Length >= 3)
                        {
                            string requestingId = parts[1].Trim();
                            string targetId = PeerResolver.ExtractRawDigitsId(parts[2].Trim());

                            if (targetId == rawNumId)
                            {
                                bool accepted = false;
                                bool isUnattended = false;
                                this.Invoke((MethodInvoker)delegate
                                {
                                    isUnattended = chkUnattendedAccess != null && chkUnattendedAccess.Checked;
                                });

                                if (isUnattended)
                                {
                                    accepted = true;
                                }
                                else
                                {
                                    this.Invoke((MethodInvoker)delegate
                                    {
                                        using (ConnectionRequestForm reqForm = new ConnectionRequestForm(requestingId))
                                        {
                                            accepted = (reqForm.ShowDialog() == DialogResult.OK && reqForm.IsAccepted);
                                        }
                                    });
                                }

                                if (!accepted)
                                {
                                    byte[] rejBuf = Encoding.UTF8.GetBytes("REJECTED\n");
                                    try { stream.Write(rejBuf, 0, rejBuf.Length); stream.Flush(); } catch { }
                                    incomingClient.Close();
                                    continue;
                                }

                                string myMachineName = Environment.MachineName;
                                byte[] okBuf = Encoding.UTF8.GetBytes(string.Format("ACCEPT_OK:{0}\n", myMachineName));
                                stream.Write(okBuf, 0, okBuf.Length);
                                stream.Flush();

                                this.Invoke((MethodInvoker)delegate
                                {
                                    if (currentFloatingWidget != null && !currentFloatingWidget.IsDisposed) currentFloatingWidget.Close();
                                    currentFloatingWidget = new HostSessionFloatingWidget(requestingId, stream, () =>
                                    {
                                        try { incomingClient.Close(); } catch { }
                                    });
                                    currentFloatingWidget.Show();
                                });

                                Thread inputReadThread = new Thread(() =>
                                {
                                    while (incomingClient.Connected && isHostRunning)
                                    {
                                        try
                                        {
                                            byte pktType;
                                            byte[] payload;
                                            if (!PacketProtocol.ReadPacket(stream, out pktType, out payload)) break;

                                            if (pktType == 0xFF) break;
                                            else if (pktType == 0x01 && payload.Length >= 9)
                                            {
                                                byte evtType = payload[0];
                                                float normX = BitConverter.ToSingle(payload, 1);
                                                float normY = BitConverter.ToSingle(payload, 5);

                                                NativeInputInjector.ExecuteMouseInput(evtType, normX, normY);
                                            }
                                            else if (pktType == 0x02 && payload.Length >= 2)
                                            {
                                                byte keyCode = payload[0];
                                                bool isDown = payload[1] == 0x01;

                                                NativeInputInjector.ExecuteKeyboardInput(keyCode, isDown);
                                            }
                                            else if (pktType == 0x03)
                                            {
                                                string chatMsg = Encoding.UTF8.GetString(payload);
                                                if (chatMsg.StartsWith("CLIENT_DISCONNECTED")) break;
                                                if (currentFloatingWidget != null && !currentFloatingWidget.IsDisposed)
                                                {
                                                    currentFloatingWidget.AppendChatMessage("Cliente Remoto", chatMsg);
                                                }
                                            }
                                            else if (pktType == 0x04)
                                            {
                                                string clipText = Encoding.UTF8.GetString(payload);
                                                this.Invoke((MethodInvoker)delegate
                                                {
                                                    try { Clipboard.SetText(clipText); } catch { }
                                                });
                                            }
                                        }
                                        catch { break; }
                                    }

                                    this.Invoke((MethodInvoker)delegate
                                    {
                                        if (currentFloatingWidget != null && !currentFloatingWidget.IsDisposed) currentFloatingWidget.Close();
                                    });
                                    try { incomingClient.Close(); } catch { }
                                }) { IsBackground = true };
                                inputReadThread.Start();

                                while (incomingClient.Connected && isHostRunning)
                                {
                                    byte[] rawFrame = DesktopCapturer.CaptureHighQualityJpeg();
                                    if (rawFrame != null && rawFrame.Length > 0)
                                    {
                                        if (!PacketProtocol.SendPacket(stream, 0x00, rawFrame)) break;
                                    }

                                    Thread.Sleep(3);
                                }

                                this.Invoke((MethodInvoker)delegate
                                {
                                    if (currentFloatingWidget != null && !currentFloatingWidget.IsDisposed) currentFloatingWidget.Close();
                                });
                            }
                            else
                            {
                                incomingClient.Close();
                            }
                        }
                    }
                }
                catch { }
            }
        }

        private void StartP2PServer()
        {
            serverThread = new Thread(ListenForIncomingConnections) { IsBackground = true };
            serverThread.Start();
        }

        private void BtnConnect_Click(object sender, EventArgs e)
        {
            string rawInput = txtRemoteId.Text.Trim();
            if (string.IsNullOrEmpty(rawInput))
            {
                MessageBox.Show("Por favor introduzca la ID remota de 9 dígitos.", "Connecting", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string pskInput = txtRemotePsk.Text.Trim();
            if (string.IsNullOrEmpty(pskInput))
            {
                MessageBox.Show("La Clave PSK es OBLIGATORIA para iniciar una sesión remota.", "Connecting", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string cleanCheck = PeerResolver.ExtractRawDigitsId(rawInput);
            if (cleanCheck == rawNumId)
            {
                MessageBox.Show("No se puede conectar a su propia ID (" + myCcId + ").\n\nAbra Connecting.exe en el otro equipo o use la ID del otro puesto.", "Connecting", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ConnectingProgressForm progressForm = new ConnectingProgressForm(rawInput);
            bool cancelRequested = false;

            Thread connThread = new Thread(() =>
            {
                try
                {
                    string errorMsg;
                    string remoteHostname;
                    TcpClient client = PeerResolver.DiscoverAndConnectPeer(rawInput, rawNumId, pskInput, out remoteHostname, out errorMsg);

                    if (client == null || !client.Connected)
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            progressForm.Close();
                            MessageBox.Show(errorMsg, "Connecting", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        });
                        return;
                    }

                    ConnectionHistoryManager.SaveRecentSession(cleanCheck, remoteHostname);

                    if (!cancelRequested)
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            progressForm.Close();
                            RemoteSessionForm sessionForm = new RemoteSessionForm(rawInput, remoteHostname, client);
                            sessionForm.FormClosed += (s2, e2) =>
                            {
                                this.Show();
                                this.WindowState = FormWindowState.Normal;
                                this.BringToFront();
                            };
                            this.Hide();
                            sessionForm.Show();
                            sessionForm.BringToFront();
                            sessionForm.Activate();
                            RefreshHistoryGrid();
                        });
                    }
                }
                catch (Exception ex)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        progressForm.Close();
                        MessageBox.Show("No se pudo conectar con el equipo remoto (" + rawInput + "): " + ex.Message, "Error Connecting", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    });
                }
            }) { IsBackground = true };

            connThread.Start();
            if (progressForm.ShowDialog() == DialogResult.Cancel)
            {
                cancelRequested = true;
            }
        }

        [STAThread]
        static void Main()
        {
            // Auto-elevación a Administrador para inyectar input en ventanas elevadas (UIPI)
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = Application.ExecutablePath;
                    psi.Verb = "runas";
                    Process.Start(psi);
                }
                catch { /* Usuario canceló UAC, continuar sin admin */ }
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
