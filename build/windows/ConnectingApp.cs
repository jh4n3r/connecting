using Conecting.Common;
using Conecting.Core;
using Conecting.Dialogs;
using Conecting.UI;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System;


[assembly: AssemblyTitle("Connecting Remote Desktop")]
[assembly: AssemblyDescription("Connecting - Portable & Secure Remote Control Platform")]
[assembly: AssemblyCompany("Connecting")]
[assembly: AssemblyProduct("Connecting Remote Desktop Enterprise")]
[assembly: AssemblyCopyright("Copyright Â© 2026 Connecting")]
[assembly: AssemblyFileVersion("1.0.2.0")]
[assembly: AssemblyVersion("1.0.2.0")]

namespace Conecting
{
    /// <summary>
    /// Application Entry Point.
    /// Initializes Visual Styles, DPI Awareness, and main Application Loop.
    /// </summary>
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                MainForm mainForm = new MainForm();
                Application.Run(mainForm);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Application Error: " + ex.Message, "Connecting", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}


namespace Conecting.Common
{
    /// <summary>
    /// Application Internationalization and Localization Manager.
    /// Provides dynamic translation helper functions (Spanish / English).
    /// </summary>
    public static class AppI18n
    {
        public static string CurrentLanguage
        {
            get { return PeerResolver.GetSavedLanguage(); }
        }

        public static bool IsEnglish
        {
            get { return CurrentLanguage == "en"; }
        }

        /// <summary>
        /// Returns localized text string based on active language setting.
        /// </summary>
        public static string T(string spanishText, string englishText)
        {
            return IsEnglish ? englishText : spanishText;
        }
    }
}


namespace Conecting.Common
{
    /// <summary>
    /// Unified packet protocol handler for streaming framing data and control messages.
    /// Header Format: [PacketType: 1 Byte][PayloadLength: 4 Bytes LittleEndian]
    /// </summary>
    public static class PacketProtocol
    {
        public static bool ReadPacket(NetworkStream stream, out byte pktType, out byte[] payload)
        {
            pktType = 0;
            payload = null;

            try
            {
                byte[] header = new byte[5];
                int readHeaderBytes = 0;
                while (readHeaderBytes < 5)
                {
                    int bytesRead = stream.Read(header, readHeaderBytes, 5 - readHeaderBytes);
                    if (bytesRead <= 0) return false;
                    readHeaderBytes += bytesRead;
                }

                pktType = header[0];
                int payloadLength = BitConverter.ToInt32(header, 1);

                if (payloadLength < 0 || payloadLength > 20971520) return false; // 20 MB safety cap

                payload = new byte[payloadLength];
                int readPayloadBytes = 0;
                while (readPayloadBytes < payloadLength)
                {
                    int bytesRead = stream.Read(payload, readPayloadBytes, payloadLength - readPayloadBytes);
                    if (bytesRead <= 0) return false;
                    readPayloadBytes += bytesRead;
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
                int payloadLen = (payload != null) ? payload.Length : 0;
                byte[] frame = new byte[5 + payloadLen];
                frame[0] = pktType;
                BitConverter.GetBytes(payloadLen).CopyTo(frame, 1);

                if (payloadLen > 0)
                {
                    Buffer.BlockCopy(payload, 0, frame, 5, payloadLen);
                }

                stream.Write(frame, 0, frame.Length);
                stream.Flush();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}


namespace Conecting.Common
{
    /// <summary>
    /// Peer Resolution Engine.
    /// Manages node ID generation, PSK keys, relay server settings, and Windows service status.
    /// </summary>
    public static class PeerResolver
    {
        private static readonly string AppDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "ConnectingNodes"
        );

        // DEFAULT RELAY SERVER DOMAIN (Configured for internal Oracle Cloud server)
        public static string RelayServerDomain = "your-relay-server.com";
        public static int RelayServerPort = 8443;

        public static string GetCustomRelayHost()
        {
            try
            {
                EnsureDirectoryExists();
                string path = Path.Combine(AppDataDirectory, "relayhost.dat");
                if (File.Exists(path))
                {
                    return File.ReadAllText(path).Trim();
                }
            }
            catch { }
            return "";
        }

        public static void SaveCustomRelayHost(string host)
        {
            try
            {
                EnsureDirectoryExists();
                string path = Path.Combine(AppDataDirectory, "relayhost.dat");
                File.WriteAllText(path, host.Trim());
            }
            catch { }
        }

        public static string GetActiveRelayHost()
        {
            string custom = GetCustomRelayHost();
            if (!string.IsNullOrEmpty(custom)) return custom;
            return RelayServerDomain;
        }

        public static string GetSavedLanguage()
        {
            try
            {
                EnsureDirectoryExists();
                string path = Path.Combine(AppDataDirectory, "language.dat");
                if (File.Exists(path))
                {
                    string saved = File.ReadAllText(path).Trim().ToLower();
                    if (saved == "en") return "en";
                }
            }
            catch { }
            return "es";
        }

        public static void SaveLanguage(string languageCode)
        {
            try
            {
                EnsureDirectoryExists();
                string path = Path.Combine(AppDataDirectory, "language.dat");
                File.WriteAllText(path, languageCode.Trim().ToLower());
            }
            catch { }
        }

        public static string GetRelayServerHost()
        {
            try
            {
                EnsureDirectoryExists();
                string path = Path.Combine(AppDataDirectory, "server_host.dat");
                if (File.Exists(path))
                {
                    string saved = File.ReadAllText(path).Trim();
                    if (!string.IsNullOrEmpty(saved)) return saved;
                }
            }
            catch { }
            return RelayServerDomain;
        }

        public static void SaveRelayServerHost(string host)
        {
            try
            {
                EnsureDirectoryExists();
                string path = Path.Combine(AppDataDirectory, "server_host.dat");
                File.WriteAllText(path, string.IsNullOrEmpty(host) ? RelayServerDomain : host.Trim());
            }
            catch { }
        }

        public static string GetUserDisplayName()
        {
            try
            {
                EnsureDirectoryExists();
                string path = Path.Combine(AppDataDirectory, "display_name.dat");
                if (File.Exists(path))
                {
                    string saved = File.ReadAllText(path).Trim();
                    if (!string.IsNullOrEmpty(saved)) return saved;
                }
            }
            catch { }
            return Environment.UserName;
        }

        public static void SaveUserDisplayName(string displayName)
        {
            try
            {
                EnsureDirectoryExists();
                string path = Path.Combine(AppDataDirectory, "display_name.dat");
                File.WriteAllText(path, string.IsNullOrEmpty(displayName) ? Environment.UserName : displayName.Trim());
            }
            catch { }
        }

        public static string GetCustomPsk()
        {
            try
            {
                EnsureDirectoryExists();
                string path = Path.Combine(AppDataDirectory, "unattended_psk.dat");
                if (File.Exists(path))
                {
                    string saved = File.ReadAllText(path).Trim();
                    if (!string.IsNullOrEmpty(saved)) return saved;
                }
            }
            catch { }
            return "";
        }

        public static void SaveCustomPsk(string pskKey)
        {
            try
            {
                EnsureDirectoryExists();
                string path = Path.Combine(AppDataDirectory, "unattended_psk.dat");
                File.WriteAllText(path, pskKey == null ? "" : pskKey.Trim());
            }
            catch { }
        }

        public static string GetPersistentId()
        {
            try
            {
                EnsureDirectoryExists();
                string path = Path.Combine(AppDataDirectory, "node_id.dat");
                if (File.Exists(path))
                {
                    string saved = File.ReadAllText(path).Trim();
                    long dummyVal;
                    if (saved.Length == 9 && long.TryParse(saved, out dummyVal)) return saved;
                }
                string newId = GenerateRandom9DigitId();
                File.WriteAllText(path, newId);
                return newId;
            }
            catch
            {
                return GenerateRandom9DigitId();
            }
        }

        public static void SavePersistentId(string nodeId)
        {
            try
            {
                EnsureDirectoryExists();
                string path = Path.Combine(AppDataDirectory, "node_id.dat");
                File.WriteAllText(path, nodeId.Trim());
            }
            catch { }
        }

        public static string GenerateRandom9DigitId()
        {
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                byte[] bytes = new byte[4];
                rng.GetBytes(bytes);
                uint val = BitConverter.ToUInt32(bytes, 0) % 900000000 + 100000000;
                return val.ToString();
            }
        }

        /// <summary>
        /// Checks whether a Windows Service is installed by reading HKLM Registry directly.
        /// Bypasses standard non-elevated OpenSCManager access denied errors.
        /// </summary>
        public static bool IsWindowsServiceInstalled(string serviceName)
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + serviceName))
                {
                    if (key != null) return true;
                }
            }
            catch { }

            try
            {
                using (Process p = new Process())
                {
                    p.StartInfo.FileName = "sc.exe";
                    p.StartInfo.Arguments = "query \"" + serviceName + "\"";
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.Start();
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    return output.Contains("SERVICE_NAME: " + serviceName) || output.Contains("STATE");
                }
            }
            catch { return false; }
        }

        public static string ExtractRawDigitsId(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < input.Length; i++)
            {
                if (char.IsDigit(input[i])) sb.Append(input[i]);
            }
            return sb.ToString();
        }

        public static TcpClient DiscoverAndConnectPeer(string targetId, string myId, string pskKey, out string remoteHostname, out string errorMsg)
        {
            remoteHostname = "PC-REMOTO";
            errorMsg = "";
            string cleanTargetId = ExtractRawDigitsId(targetId);

            if (cleanTargetId.Length != 9)
            {
                errorMsg = AppI18n.T("La ID introducida debe contener exactamente 9 dÃ­gitos.", "Entered ID must contain exactly 9 digits.");
                return null;
            }

            try
            {
                TcpClient client = new TcpClient();
                client.NoDelay = true;
                client.SendBufferSize = 262144;
                client.ReceiveBufferSize = 262144;

                string targetHost = GetRelayServerHost();
                IAsyncResult ar = client.BeginConnect(targetHost, RelayServerPort, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(3000) || !client.Connected)
                {
                    try { client.Close(); } catch { }
                    errorMsg = string.Format(AppI18n.T(
                        "No se pudo establecer conexiÃ³n con el Servidor Relay ({0}:{1}).",
                        "Could not connect to Relay Server ({0}:{1})."
                    ), targetHost, RelayServerPort);
                    return null;
                }

                NetworkStream ns = client.GetStream();
                byte[] handshakeBytes = Encoding.UTF8.GetBytes(string.Format("CONNECT:{0}:{1}:{2}\n", myId, cleanTargetId, pskKey));
                ns.Write(handshakeBytes, 0, handshakeBytes.Length);
                ns.Flush();

                byte[] responseBuf = new byte[256];
                int r = ns.Read(responseBuf, 0, 256);
                if (r <= 0)
                {
                    client.Close();
                    errorMsg = AppI18n.T("El equipo remoto no respondiÃ³ a la solicitud.", "Remote computer did not respond to request.");
                    return null;
                }

                string resp = Encoding.UTF8.GetString(responseBuf, 0, r).Trim();
                if (resp.StartsWith("ACCEPT_OK"))
                {
                    if (resp.Contains(":"))
                    {
                        remoteHostname = resp.Split(':')[1].Trim();
                    }
                    return client;
                }
                else if (resp.Contains("BUSY"))
                {
                    client.Close();
                    errorMsg = AppI18n.T("El equipo remoto se encuentra en otra sesiÃ³n activa.", "Remote computer is busy in another active session.");
                    return null;
                }
                else if (resp.Contains("PSK_INVALID"))
                {
                    client.Close();
                    errorMsg = AppI18n.T("La Clave PSK introducida es incorrecta.", "The entered PSK Key is incorrect.");
                    return null;
                }
                else
                {
                    client.Close();
                    errorMsg = string.Format(AppI18n.T(
                        "El equipo remoto ID ({0}) estÃ¡ fuera de lÃ­nea o rechazÃ³ la conexiÃ³n.",
                        "Remote computer ID ({0}) is offline or rejected connection."
                    ), cleanTargetId);
                    return null;
                }
            }
            catch (Exception ex)
            {
                errorMsg = AppI18n.T("Error de conexiÃ³n: ", "Connection error: ") + ex.Message;
                return null;
            }
        }

        private static void EnsureDirectoryExists()
        {
            if (!Directory.Exists(AppDataDirectory))
            {
                Directory.CreateDirectory(AppDataDirectory);
            }
        }
    }
}


namespace Conecting.Core
{
    public class HistoryItem
    {
        public string Id { get; set; }
        public string Hostname { get; set; }
        public string Alias { get; set; }
        public DateTime LastConnected { get; set; }
    }

    /// <summary>
    /// Connection History Storage and Persistence Engine.
    /// Manages recently connected workstations in local JSON format.
    /// </summary>
    public static class ConnectionHistoryManager
    {
        private static readonly string AppDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "ConnectingNodes"
        );
        private static readonly string HistoryFilePath = Path.Combine(AppDataDirectory, "history.json");

        public static List<HistoryItem> GetRecentSessions()
        {
            List<HistoryItem> items = new List<HistoryItem>();
            try
            {
                if (File.Exists(HistoryFilePath))
                {
                    string json = File.ReadAllText(HistoryFilePath);
                    items = ParseHistoryJson(json);
                }
            }
            catch { }

            items.Sort((a, b) => b.LastConnected.CompareTo(a.LastConnected));
            return items;
        }

        public static void SaveRecentSession(string nodeId, string hostname)
        {
            if (string.IsNullOrEmpty(nodeId) || nodeId.Length != 9) return;
            try
            {
                List<HistoryItem> items = GetRecentSessions();
                HistoryItem existing = items.Find(x => x.Id == nodeId);
                if (existing != null)
                {
                    existing.LastConnected = DateTime.Now;
                    if (!string.IsNullOrEmpty(hostname)) existing.Hostname = hostname;
                }
                else
                {
                    items.Add(new HistoryItem
                    {
                        Id = nodeId,
                        Hostname = string.IsNullOrEmpty(hostname) ? "PC-REMOTO" : hostname,
                        Alias = "",
                        LastConnected = DateTime.Now
                    });
                }

                if (items.Count > 20)
                {
                    items.RemoveRange(20, items.Count - 20);
                }

                SaveHistoryList(items);
            }
            catch { }
        }

        public static void UpdateAlias(string nodeId, string newAlias)
        {
            try
            {
                List<HistoryItem> items = GetRecentSessions();
                HistoryItem existing = items.Find(x => x.Id == nodeId);
                if (existing != null)
                {
                    existing.Alias = newAlias == null ? "" : newAlias.Trim();
                    SaveHistoryList(items);
                }
            }
            catch { }
        }

        public static void RemoveSession(string nodeId)
        {
            try
            {
                List<HistoryItem> items = GetRecentSessions();
                items.RemoveAll(x => x.Id == nodeId);
                SaveHistoryList(items);
            }
            catch { }
        }

        public static void ClearAll()
        {
            try
            {
                if (File.Exists(HistoryFilePath))
                {
                    File.Delete(HistoryFilePath);
                }
            }
            catch { }
        }

        private static void SaveHistoryList(List<HistoryItem> items)
        {
            try
            {
                if (!Directory.Exists(AppDataDirectory)) Directory.CreateDirectory(AppDataDirectory);
                string json = SerializeHistoryJson(items);
                File.WriteAllText(HistoryFilePath, json);
            }
            catch { }
        }

        private static string SerializeHistoryJson(List<HistoryItem> items)
        {
            List<string> jsonItems = new List<string>();
            foreach (var item in items)
            {
                string safeAlias = item.Alias == null ? "" : item.Alias.Replace("\"", "\\\"");
                string safeHost = item.Hostname == null ? "PC-REMOTO" : item.Hostname.Replace("\"", "\\\"");
                jsonItems.Add(string.Format("{{\"id\":\"{0}\",\"host\":\"{1}\",\"alias\":\"{2}\",\"date\":\"{3}\"}}", 
                    item.Id, safeHost, safeAlias, item.LastConnected.ToString("o")));
            }
            return "[" + string.Join(",", jsonItems.ToArray()) + "]";
        }

        private static List<HistoryItem> ParseHistoryJson(string json)
        {
            List<HistoryItem> list = new List<HistoryItem>();
            if (string.IsNullOrEmpty(json) || !json.StartsWith("[")) return list;

            try
            {
                string inner = json.Trim('[', ']');
                if (string.IsNullOrEmpty(inner)) return list;

                string[] blocks = inner.Split(new string[] { "},{" }, StringSplitOptions.None);
                foreach (string b in blocks)
                {
                    string clean = b.Trim('{', '}');
                    string[] kvPairs = clean.Split(',');
                    HistoryItem item = new HistoryItem { LastConnected = DateTime.Now, Hostname = "PC-REMOTO", Alias = "" };
                    foreach (string kv in kvPairs)
                    {
                        string[] parts = kv.Split(new char[] { ':' }, 2);
                        if (parts.Length == 2)
                        {
                            string key = parts[0].Trim('"', ' ');
                            string val = parts[1].Trim('"', ' ');
                            if (key == "id") item.Id = val;
                            else if (key == "host") item.Hostname = val;
                            else if (key == "alias") item.Alias = val;
                            else if (key == "date")
                            {
                                DateTime dt;
                                if (DateTime.TryParse(val, out dt)) item.LastConnected = dt;
                            }
                        }
                    }
                    if (!string.IsNullOrEmpty(item.Id)) list.Add(item);
                }
            }
            catch { }

            return list;
        }
    }
}


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


namespace Conecting.Core
{
    /// <summary>
    /// High-Precision Native Win32 Input Injector.
    /// Uses official Win32 SendInput API for UIPI and Admin elevation compliance.
    /// </summary>
    public static class NativeInputInjector
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        public static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        public const uint INPUT_MOUSE = 0;
        public const uint INPUT_KEYBOARD = 1;

        public const uint MOUSEEVENTF_MOVE = 0x0001;
        public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP = 0x0004;
        public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        public const uint MOUSEEVENTF_WHEEL = 0x0800;
        public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
        public const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;

        public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const uint KEYEVENTF_SCANCODE = 0x0004;

        /// <summary>
        /// Executes mouse movement, click, drag, or scroll using Win32 SendInput.
        /// </summary>
        public static void ExecuteMouseInput(byte eventType, float normalizedX, float normalizedY)
        {
            try
            {
                if (eventType == 0x06) // Mouse Wheel Up
                {
                    InjectMouseWheel(120);
                    return;
                }
                else if (eventType == 0x07) // Mouse Wheel Down
                {
                    InjectMouseWheel(-120);
                    return;
                }

                int screenWidth = Screen.PrimaryScreen.Bounds.Width;
                int screenHeight = Screen.PrimaryScreen.Bounds.Height;
                int targetPixelX = (int)(normalizedX * screenWidth);
                int targetPixelY = (int)(normalizedY * screenHeight);

                SetCursorPos(targetPixelX, targetPixelY);

                uint absoluteX = (uint)(normalizedX * 65535.0f);
                uint absoluteY = (uint)(normalizedY * 65535.0f);

                uint flags = MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK | MOUSEEVENTF_MOVE;

                if (eventType == 0x02) // Left Button Down
                {
                    flags |= MOUSEEVENTF_LEFTDOWN;
                }
                else if (eventType == 0x03) // Left Button Up
                {
                    flags |= MOUSEEVENTF_LEFTUP;
                }
                else if (eventType == 0x04) // Right Button Down
                {
                    flags |= MOUSEEVENTF_RIGHTDOWN;
                }
                else if (eventType == 0x05) // Right Button Up
                {
                    flags |= MOUSEEVENTF_RIGHTUP;
                }
                else if (eventType == 0x01) // Move / Drag
                {
                    // For movement, only MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK | MOUSEEVENTF_MOVE is required.
                }

                INPUT[] inputs = new INPUT[1];
                inputs[0].type = INPUT_MOUSE;
                inputs[0].U.mi.dx = (int)absoluteX;
                inputs[0].U.mi.dy = (int)absoluteY;
                inputs[0].U.mi.dwFlags = flags;
                inputs[0].U.mi.mouseData = 0;
                inputs[0].U.mi.time = 0;
                inputs[0].U.mi.dwExtraInfo = IntPtr.Zero;

                SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
            }
            catch { }
        }

        private static void InjectMouseWheel(int scrollDelta)
        {
            try
            {
                INPUT[] inputs = new INPUT[1];
                inputs[0].type = INPUT_MOUSE;
                inputs[0].U.mi.dx = 0;
                inputs[0].U.mi.dy = 0;
                inputs[0].U.mi.dwFlags = MOUSEEVENTF_WHEEL;
                inputs[0].U.mi.mouseData = unchecked((uint)scrollDelta);
                inputs[0].U.mi.time = 0;
                inputs[0].U.mi.dwExtraInfo = IntPtr.Zero;

                SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
            }
            catch { }
        }

        /// <summary>
        /// Executes keyboard press or release using Win32 SendInput.
        /// </summary>
        public static void ExecuteKeyboardInput(byte virtualKeyCode, bool isKeyDown)
        {
            try
            {
                ushort scanCode = (ushort)MapVirtualKey(virtualKeyCode, 0);
                uint flags = isKeyDown ? 0u : KEYEVENTF_KEYUP;

                if ((virtualKeyCode >= 0x21 && virtualKeyCode <= 0x28) || 
                    virtualKeyCode == 0x2C || virtualKeyCode == 0x2D || virtualKeyCode == 0x2E || 
                    virtualKeyCode == 0x5B || virtualKeyCode == 0x5C || virtualKeyCode == 0xA3 || virtualKeyCode == 0xA5)
                {
                    flags |= KEYEVENTF_EXTENDEDKEY;
                }

                INPUT[] inputs = new INPUT[1];
                inputs[0].type = INPUT_KEYBOARD;
                inputs[0].U.ki.wVk = virtualKeyCode;
                inputs[0].U.ki.wScan = scanCode;
                inputs[0].U.ki.dwFlags = flags;
                inputs[0].U.ki.time = 0;
                inputs[0].U.ki.dwExtraInfo = IntPtr.Zero;

                SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
            }
            catch { }
        }
    }
}


namespace Conecting.Dialogs
{
    public class AboutForm : Form
    {
        public AboutForm()
        {
            this.Text = AppI18n.T("Acerca de Connecting", "About Connecting");
            this.Size = new Size(420, 260);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.White;

            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Label lblTitle = new Label { Text = "Connecting Remote Desktop", Font = new Font("Segoe UI", 14F, FontStyle.Bold), Location = new Point(20, 20), AutoSize = true, ForeColor = Color.FromArgb(14, 98, 115) };
            Label lblVer = new Label { Text = AppI18n.T("VersiÃ³n 1.0.2 (Build 2026)", "Version 1.0.2 (Build 2026)"), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), Location = new Point(20, 52), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) };
            Label lblDesc = new Label
            {
                Text = AppI18n.T(
                    "Plataforma de Escritorio Remoto Abierta, Portable y Segura.\nDiseÃ±ado para ofrecer asistencia tÃ©cnica nativa sin dependencias.",
                    "Open, Portable, and Secure Remote Desktop Platform.\nDesigned for native technical support without external dependencies."
                ),
                Font = new Font("Segoe UI", 9.5F),
                Location = new Point(20, 85),
                Size = new Size(360, 60)
            };

            ModernButton btnOk = new ModernButton
            {
                Text = AppI18n.T("Aceptar", "OK"),
                Location = new Point(280, 165),
                Size = new Size(100, 36),
                NormalColor = Color.FromArgb(14, 98, 115),
                HoverColor = Color.FromArgb(8, 70, 84),
                BorderRadius = 6
            };
            btnOk.Click += (s, e) => { this.Close(); };

            this.Controls.Add(lblTitle);
            this.Controls.Add(lblVer);
            this.Controls.Add(lblDesc);
            this.Controls.Add(btnOk);
        }
    }
}


namespace Conecting.Dialogs
{
    public class ConnectionRequestForm : Form
    {
        private ModernButton btnAccept;
        private ModernButton btnReject;
        public bool IsAccepted { get; private set; }

        public ConnectionRequestForm(string requestingPeerId)
        {
            this.Text = AppI18n.T("Solicitud de ConexiÃ³n Entrante", "Incoming Connection Request");
            this.Size = new Size(480, 310);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.TopMost = true;
            this.BackColor = Color.White;

            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Panel topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(241, 245, 249),
                Padding = new Padding(16)
            };

            PictureBox picIcon = new PictureBox { Size = new Size(48, 48), Location = new Point(16, 16) };
            Bitmap bmp = new Bitmap(48, 48);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen p = new Pen(Color.FromArgb(14, 98, 115), 3f)) g.DrawEllipse(p, 4, 4, 40, 40);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(14, 98, 115))) g.FillRectangle(b, 14, 14, 20, 14);
            }
            picIcon.Image = bmp;

            Label lblTitle = new Label
            {
                Text = AppI18n.T("Â¡Solicitud de Control Remoto!", "Remote Control Request!"),
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Location = new Point(75, 16),
                AutoSize = true,
                ForeColor = Color.FromArgb(15, 23, 42)
            };

            Label lblSub = new Label
            {
                Text = string.Format(AppI18n.T("El puesto ID ({0}) desea conectarse a este equipo.", "Workstation ID ({0}) wants to connect to this computer."), requestingPeerId),
                Font = new Font("Segoe UI", 9.5F),
                Location = new Point(75, 42),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 116, 139)
            };

            topPanel.Controls.Add(picIcon);
            topPanel.Controls.Add(lblTitle);
            topPanel.Controls.Add(lblSub);

            GroupBox boxPerms = new GroupBox
            {
                Text = AppI18n.T(" Permisos Concedidos ", " Granted Permissions "),
                Location = new Point(20, 95),
                Size = new Size(424, 105),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };

            CheckBox chkInput = new CheckBox
            {
                Text = AppI18n.T("Controlar teclado y ratÃ³n en tiempo real", "Control mouse and keyboard in real time"),
                Checked = true,
                Location = new Point(16, 30),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F)
            };

            CheckBox chkClipboard = new CheckBox
            {
                Text = AppI18n.T("Acceder al portapapeles bidireccional", "Access real-time bidirectional clipboard"),
                Checked = true,
                Location = new Point(16, 65),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F)
            };

            boxPerms.Controls.Add(chkInput);
            boxPerms.Controls.Add(chkClipboard);

            btnAccept = new ModernButton
            {
                Text = AppI18n.T("ACEPTAR", "ACCEPT"),
                Location = new Point(20, 215),
                Size = new Size(200, 44),
                NormalColor = Color.FromArgb(16, 185, 129),
                HoverColor = Color.FromArgb(5, 150, 105),
                BorderRadius = 6
            };
            btnAccept.Click += (s, e) => { IsAccepted = true; this.DialogResult = DialogResult.OK; this.Close(); };

            btnReject = new ModernButton
            {
                Text = AppI18n.T("RECHAZAR", "REJECT"),
                Location = new Point(244, 215),
                Size = new Size(200, 44),
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
}


namespace Conecting.Dialogs
{
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
                using (Pen p = new Pen(Color.FromArgb(14, 98, 115), 3.5f)) g.DrawArc(p, 4, 4, 40, 40, 0, 270);
            }
            picIcon.Image = bmp;

            Label lblHeader = new Label
            {
                Text = string.Format(AppI18n.T("Conectando a {0}...", "Connecting to {0}..."), remoteId),
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Location = new Point(85, 24),
                AutoSize = true,
                ForeColor = Color.FromArgb(15, 23, 42)
            };

            Label lblSubText = new Label
            {
                Text = AppI18n.T("Estableciendo conexiÃ³n en tiempo real...\nEsperando respuesta del equipo remoto.", "Establishing real-time connection...\nWaiting for remote computer response."),
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Color.FromArgb(100, 116, 139),
                Location = new Point(85, 55),
                AutoSize = true
            };

            btnCancel = new ModernButton
            {
                Text = AppI18n.T("Cancelar", "Cancel"),
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
}


namespace Conecting.UI
{
    /// <summary>
    /// Floating Session Notification Widget on Host Screen.
    /// Shows connected client ID, active status, chat drawer toggle, and End Session button.
    /// </summary>
    public class HostSessionFloatingWidget : Form
    {
        private Action onCloseCallback;
        private Label lblStatus;
        private ModernButton btnDisconnect;
        private ModernButton btnToggleChat;
        private RichTextBox txtChatHistory;
        private TextBox txtChatMessage;
        private ModernButton btnSendChat;
        private NetworkStream activeStream;

        public HostSessionFloatingWidget(string requestingId, NetworkStream stream, Action onClose)
        {
            this.activeStream = stream;
            this.onCloseCallback = onClose;

            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.Size = new Size(270, 48);
            this.BackColor = Color.FromArgb(15, 23, 42);

            Rectangle workArea = Screen.PrimaryScreen.WorkingArea;
            this.Location = new Point(workArea.Right - 290, workArea.Bottom - 68);

            lblStatus = new Label
            {
                Text = string.Format("ID: {0}", requestingId),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(74, 222, 128),
                Location = new Point(10, 6),
                AutoSize = true
            };

            Label lblSub = new Label
            {
                Text = AppI18n.T("SESIÃ“N ACTIVA", "ACTIVE SESSION"),
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(10, 24),
                AutoSize = true
            };

            btnToggleChat = new ModernButton
            {
                Text = "Chat",
                Location = new Point(135, 10),
                Size = new Size(50, 28),
                NormalColor = Color.FromArgb(0, 172, 193),
                HoverColor = Color.FromArgb(0, 131, 143),
                BorderRadius = 4,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            btnToggleChat.Click += (s, e) => { ToggleChatDrawer(); };

            btnDisconnect = new ModernButton
            {
                Text = AppI18n.T("Finalizar", "End"),
                Location = new Point(190, 10),
                Size = new Size(70, 28),
                NormalColor = Color.FromArgb(239, 68, 68),
                HoverColor = Color.FromArgb(220, 38, 38),
                BorderRadius = 4,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            btnDisconnect.Click += (s, e) =>
            {
                if (chatDrawerForm != null && !chatDrawerForm.IsDisposed) chatDrawerForm.Close();
                if (onCloseCallback != null) onCloseCallback();
                this.Close();
            };

            this.Controls.Add(lblStatus);
            this.Controls.Add(lblSub);
            this.Controls.Add(btnToggleChat);
            this.Controls.Add(btnDisconnect);

            BuildChatDrawer();
        }

        private Form chatDrawerForm;

        private void BuildChatDrawer()
        {
            chatDrawerForm = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                TopMost = true,
                ShowInTaskbar = false,
                Size = new Size(270, 220),
                Location = new Point(this.Location.X, this.Location.Y - 225),
                BackColor = Color.FromArgb(15, 23, 42)
            };

            txtChatHistory = new RichTextBox
            {
                Location = new Point(8, 8),
                Size = new Size(254, 170),
                ReadOnly = true,
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 8.5F)
            };

            txtChatMessage = new TextBox
            {
                Location = new Point(8, 185),
                Size = new Size(190, 26),
                Font = new Font("Segoe UI", 9F)
            };
            txtChatMessage.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SendChat(); } };

            btnSendChat = new ModernButton
            {
                Text = "Send",
                Location = new Point(202, 184),
                Size = new Size(60, 26),
                NormalColor = Color.FromArgb(0, 172, 193),
                HoverColor = Color.FromArgb(0, 131, 143),
                BorderRadius = 4,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };
            btnSendChat.Click += (s, e) => { SendChat(); };

            chatDrawerForm.Controls.Add(txtChatHistory);
            chatDrawerForm.Controls.Add(txtChatMessage);
            chatDrawerForm.Controls.Add(btnSendChat);
        }

        private void ToggleChatDrawer()
        {
            if (chatDrawerForm == null) return;
            if (chatDrawerForm.Visible)
            {
                chatDrawerForm.Hide();
            }
            else
            {
                chatDrawerForm.Location = new Point(this.Location.X, this.Location.Y - 225);
                chatDrawerForm.Show();
                chatDrawerForm.BringToFront();
                txtChatMessage.Focus();
            }
        }

        public void AppendChatMessage(string senderName, string message)
        {
            try
            {
                if (this.IsDisposed) return;
                this.Invoke((MethodInvoker)delegate
                {
                    txtChatHistory.AppendText(senderName + ": " + message + "\n");
                    txtChatHistory.ScrollToCaret();
                    if (chatDrawerForm != null && !chatDrawerForm.Visible)
                    {
                        chatDrawerForm.Location = new Point(this.Location.X, this.Location.Y - 225);
                        chatDrawerForm.Show();
                        chatDrawerForm.BringToFront();
                    }
                });
            }
            catch { }
        }

        private void SendChat()
        {
            string msg = txtChatMessage.Text.Trim();
            if (string.IsNullOrEmpty(msg)) return;

            string myName = PeerResolver.GetUserDisplayName();
            txtChatHistory.AppendText(myName + " (Host): " + msg + "\n");
            txtChatHistory.ScrollToCaret();
            txtChatMessage.Clear();

            try
            {
                if (activeStream != null)
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(myName + ": " + msg);
                    PacketProtocol.SendPacket(activeStream, 0x03, bytes);
                }
            }
            catch { }
        }
    }
}


namespace Conecting.UI
{
    /// <summary>
    /// Main Application Window.
    /// Manages local node ID registration, host listener loop, settings, and SessionTabControl multi-tab container.
    /// </summary>
    public class MainForm : Form
    {
        private SessionTabControl sessionTabControl;

        private Panel topHeader;
        private Panel panelNavHeader;
        private Panel panelContentDashboard;
        private Panel panelContentSettings;

        private Label lblTitle;
        private Label lblSubtitle;
        private Label lblMyToken;
        private Label lblPskToken;

        private ModernCardPanel cardLocalToken;
        private ModernCardPanel cardRemoteConnect;
        private ModernCardPanel cardHistory;
        private ModernCardPanel cardSec;
        private ModernCardPanel cardService;

        private ModernButton btnCopyId;
        private ModernButton btnRegenerateId;
        private ModernButton btnConnect;
        private TextBox txtRemoteId;
        private TextBox txtRemotePsk;
        private TextBox txtCustomPsk;
        private CheckBox chkUnattendedAccess;
        private FlowLayoutPanel flowHistory;

        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private bool allowExit = false;

        private string myCcId = "000 000 000";
        private string rawNumId = "000000000";
        private string myPskToken = "123456";
        private int myBoundPort = 9000;

        private TcpListener tcpListener;
        private Thread serverThread;
        private Thread relayRegistrationThread;
        private bool isHostRunning = true;

        private TcpClient currentHostRelayClient;
        private HostSessionFloatingWidget currentFloatingWidget;

        private static readonly Color ColorBg = Color.FromArgb(248, 250, 252);
        private static readonly Color ColorCardBg = Color.White;
        private static readonly Color ColorCyanPrimary = Color.FromArgb(14, 98, 115);
        private static readonly Color ColorCyanDark = Color.FromArgb(8, 70, 84);
        private static readonly Color ColorTextDark = Color.FromArgb(15, 23, 42);
        private static readonly Color ColorTextMuted = Color.FromArgb(100, 116, 139);

        public MainForm()
        {
            GenerateMyCredentials(false);
            InitializeComponent();
            SetupSystemTray();
            StartP2PServer();
            StartRelayHostRegistration();
        }

        private void SetupSystemTray()
        {
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add(AppI18n.T("Abrir Connecting", "Open Connecting"), null, (s, e) => { RestoreFromTray(); });
            trayMenu.Items.Add(string.Format("ID: {0}", myCcId), null, (s, e) => { Clipboard.SetText(rawNumId); });
            trayMenu.Items.Add("-");
            trayMenu.Items.Add(AppI18n.T("Salir", "Exit"), null, (s, e) => { allowExit = true; Application.Exit(); });

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
                    trayIcon.ShowBalloonTip(2000, "Connecting", AppI18n.T("La aplicaciÃ³n sigue activa en segundo plano.", "Connecting is running in system tray."), ToolTipIcon.Info);
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
            if (lblPskToken != null) lblPskToken.Text = AppI18n.T("Clave PSK Segura: ", "Secure PSK Key: ") + myPskToken;
            if (trayMenu != null && trayMenu.Items.Count > 1) trayMenu.Items[1].Text = string.Format("ID: {0}", myCcId);
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
                        IAsyncResult ar = relayClient.BeginConnect(targetHost, PeerResolver.RelayServerPort, null, null);
                        if (ar.AsyncWaitHandle.WaitOne(2500) && relayClient.Connected)
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
                                        activeRelayClient.NoDelay = true;
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
                                                    else if (pktType == 0x05 && payload.Length > 0)
                                                    {
                                                        string qStr = Encoding.UTF8.GetString(payload).Trim();
                                                        long qVal;
                                                        if (long.TryParse(qStr, out qVal))
                                                        {
                                                            DesktopCapturer.SetJpegQuality(qVal);
                                                        }
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
            this.Text = "Connecting - SoluciÃ³n de Escritorio Remoto";
            this.Size = new Size(1000, 750);
            this.MinimumSize = new Size(950, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = ColorBg;
            this.Font = new Font("Segoe UI", 10F);

            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            topHeader = new Panel
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
                using (SolidBrush brushFill = new SolidBrush(Color.FromArgb(238, 242, 246))) g.FillEllipse(brushFill, 5, 5, 37, 37);
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
                Font = new Font("Segoe UI", 22F, FontStyle.Bold),
                ForeColor = ColorCyanPrimary,
                Location = new Point(84, 14),
                AutoSize = true
            };

            lblSubtitle = new Label
            {
                Text = AppI18n.T("Plataforma de Control Remoto PortÃ¡til y Segura", "Portable and Secure Remote Desktop Platform"),
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = ColorTextMuted,
                Location = new Point(87, 54),
                AutoSize = true
            };

            panelNavHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 42,
                BackColor = Color.White,
                Padding = new Padding(12, 4, 12, 4)
            };

            FlowLayoutPanel flowNavTabs = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                WrapContents = false,
                BackColor = Color.White,
                Padding = new Padding(0)
            };

            // AnyDesk-Style Hamburger Menu Button [ â‰¡ ]
            ModernButton btnHamburgerMenu = new ModernButton
            {
                Text = " â‰¡ ",
                Dock = DockStyle.Right,
                Width = 44,
                NormalColor = Color.FromArgb(241, 245, 249),
                HoverColor = Color.FromArgb(226, 232, 240),
                ForeColor = ColorTextDark,
                BorderRadius = 6,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold)
            };

            ContextMenuStrip menuHamburger = new ContextMenuStrip();
            menuHamburger.Items.Add(AppI18n.T("âš™ ConfiguraciÃ³n & Seguridad", "âš™ Settings & Security"), null, (s, e) =>
            {
                if (sessionTabControl != null) sessionTabControl.SelectSettingsTab();
            });

            bool isAdmin = IsUserAnAdmin();
            menuHamburger.Items.Add(isAdmin ? AppI18n.T("ðŸ›¡ Modo Administrador (Activo)", "ðŸ›¡ Administrator Mode (Active)") : AppI18n.T("ðŸ›¡ Reiniciar como Administrador", "ðŸ›¡ Restart as Administrator"), null, (s, e) =>
            {
                if (!isAdmin)
                {
                    if (MessageBox.Show(AppI18n.T("Â¿Desea reiniciar la aplicaciÃ³n con permisos de Administrador?", "Restart application with Administrator permissions?"), "Connecting", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        try
                        {
                            ProcessStartInfo psi = new ProcessStartInfo
                            {
                                FileName = Application.ExecutablePath,
                                Verb = "runas",
                                UseShellExecute = true
                            };
                            Process.Start(psi);
                            Application.Exit();
                        }
                        catch { }
                    }
                }
            });
            menuHamburger.Items.Add("-");
            menuHamburger.Items.Add(AppI18n.T("â“ Acerca de Connecting...", "â“ About Connecting..."), null, (s, e) =>
            {
                using (AboutForm about = new AboutForm()) { about.ShowDialog(); }
            });
            menuHamburger.Items.Add(AppI18n.T("ðŸ“– Ayuda y DocumentaciÃ³n", "ðŸ“– Help & Documentation"), null, (s, e) =>
            {
                try { Process.Start("https://jh4n3r.github.io/connecting/docs/"); } catch { }
            });

            btnHamburgerMenu.Click += (s, e) =>
            {
                menuHamburger.Show(btnHamburgerMenu, new Point(0, btnHamburgerMenu.Height));
            };

            topHeader.Controls.Add(picLogo);
            topHeader.Controls.Add(lblTitle);
            topHeader.Controls.Add(lblSubtitle);

            panelNavHeader.Controls.Add(flowNavTabs);
            panelNavHeader.Controls.Add(btnHamburgerMenu);

            Panel contentMainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorBg
            };

            panelContentDashboard = new Panel { Dock = DockStyle.Fill, BackColor = ColorBg, Padding = new Padding(24), AutoScroll = true };
            panelContentSettings = new Panel { Dock = DockStyle.Fill, BackColor = ColorBg, Padding = new Padding(24), Visible = false };

            BuildDashboardTab();
            BuildSettingsTab();

            // Initialize Unified Multi-Session Tab Control
            sessionTabControl = new SessionTabControl(flowNavTabs, contentMainContainer, panelContentDashboard, panelContentSettings, topHeader);

            this.Controls.Add(contentMainContainer);
            this.Controls.Add(panelNavHeader);
            this.Controls.Add(topHeader);
        }

        private static bool IsUserAnAdmin()
        {
            try
            {
                System.Security.Principal.WindowsIdentity id = System.Security.Principal.WindowsIdentity.GetCurrent();
                System.Security.Principal.WindowsPrincipal principal = new System.Security.Principal.WindowsPrincipal(id);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        private void BuildDashboardTab()
        {
            bool isAdmin = IsUserAnAdmin();
            cardLocalToken = new ModernCardPanel
            {
                Size = new Size(930, 180),
                Location = new Point(24, 20),
                BackColor = ColorCardBg,
                BorderRadius = 16
            };

            Label lblBadgeId = new Label
            {
                Text = "TU ID",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = ColorCyanPrimary,
                Location = new Point(24, 18),
                AutoSize = true
            };

            Label lblMyTokenTitle = new Label
            {
                Text = AppI18n.T("Tu ID de Acceso Permanente (Este Puesto):", "Your Permanent Access ID (This Workstation):"),
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = ColorTextDark,
                Location = new Point(24, 38),
                AutoSize = true
            };

            lblMyToken = new Label
            {
                Text = myCcId,
                Font = new Font("Segoe UI", 34F, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                Location = new Point(20, 64),
                AutoSize = true
            };

            lblPskToken = new Label
            {
                Text = AppI18n.T("Clave PSK Segura: ", "Secure PSK Key: ") + myPskToken,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(225, 29, 72),
                Location = new Point(24, 134),
                AutoSize = true
            };

            btnCopyId = new ModernButton
            {
                Text = AppI18n.T("Copiar ID", "Copy ID"),
                Location = new Point(590, 75),
                Size = new Size(140, 44),
                NormalColor = Color.FromArgb(241, 245, 249),
                HoverColor = Color.FromArgb(226, 232, 240),
                ForeColor = ColorTextDark,
                BorderRadius = 10
            };
            btnCopyId.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetText(rawNumId);
                    MessageBox.Show(string.Format(AppI18n.T("ID ({0}) copiada al portapapeles.", "ID ({0}) copied to clipboard."), rawNumId), "Connecting", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch { }
            };

            btnRegenerateId = new ModernButton
            {
                Text = AppI18n.T("Regenerar ID", "Regenerate ID"),
                Location = new Point(740, 75),
                Size = new Size(150, 44),
                NormalColor = Color.FromArgb(254, 242, 242),
                HoverColor = Color.FromArgb(254, 226, 226),
                ForeColor = Color.FromArgb(225, 29, 72),
                BorderRadius = 10
            };
            btnRegenerateId.Click += (s, e) =>
            {
                if (MessageBox.Show(AppI18n.T("Â¿EstÃ¡ seguro de que desea generar una nueva ID permanente para este puesto?", "Are you sure you want to generate a new permanent ID for this computer?"), "Connecting", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    GenerateMyCredentials(true);
                }
            };

            cardLocalToken.Controls.Add(lblBadgeId);
            cardLocalToken.Controls.Add(lblMyTokenTitle);
            cardLocalToken.Controls.Add(lblMyToken);
            cardLocalToken.Controls.Add(lblPskToken);
            cardLocalToken.Controls.Add(btnCopyId);
            cardLocalToken.Controls.Add(btnRegenerateId);

            cardRemoteConnect = new ModernCardPanel
            {
                Size = new Size(930, 185),
                Location = new Point(24, 215),
                BackColor = ColorCardBg,
                BorderRadius = 16
            };

            Label lblRemoteTitle = new Label
            {
                Text = AppI18n.T("Conectar a (ID de Escritorio):", "Connect to (Desktop ID):"),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = ColorTextDark,
                Location = new Point(24, 18),
                AutoSize = true
            };

            ModernInputContainer inputContainerId = new ModernInputContainer
            {
                Location = new Point(24, 46),
                Size = new Size(620, 46),
                BorderRadius = 10
            };

            txtRemoteId = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 14F),
                ForeColor = ColorTextDark,
                BorderStyle = BorderStyle.None,
                Text = ""
            };
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
                Text = AppI18n.T("Clave PSK (Obligatoria):", "PSK Key (Required):"),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = ColorTextDark,
                Location = new Point(24, 118),
                AutoSize = true
            };

            ModernInputContainer inputContainerPsk = new ModernInputContainer
            {
                Location = new Point(210, 110),
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
                Text = AppI18n.T("CONECTAR", "CONNECT"),
                Location = new Point(660, 46),
                Size = new Size(230, 46),
                NormalColor = ColorCyanPrimary,
                HoverColor = ColorCyanDark,
                ForeColor = Color.White,
                BorderRadius = 23
            };
            btnConnect.Click += BtnConnect_Click;

            ModernButton btnTopAdmin = new ModernButton
            {
                Text = isAdmin ? AppI18n.T("Modo Admin (Activo)", "Admin Mode (Active)") : AppI18n.T("Reiniciar como Admin", "Restart as Admin"),
                Location = new Point(660, 108),
                Size = new Size(230, 44),
                NormalColor = isAdmin ? Color.FromArgb(22, 163, 74) : Color.FromArgb(245, 158, 11),
                HoverColor = isAdmin ? Color.FromArgb(22, 163, 74) : Color.FromArgb(217, 119, 6),
                ForeColor = Color.White,
                BorderRadius = 10
            };
            if (!isAdmin)
            {
                btnTopAdmin.Click += (s, e) =>
                {
                    if (MessageBox.Show(AppI18n.T("Â¿Desea reiniciar la aplicaciÃ³n con permisos elevados de Administrador?", "Restart application with elevated Administrator permissions?"), "Connecting", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        try
                        {
                            if (trayIcon != null) { trayIcon.Visible = false; trayIcon.Dispose(); }
                            ProcessStartInfo psi = new ProcessStartInfo
                            {
                                FileName = Application.ExecutablePath,
                                Verb = "runas",
                                UseShellExecute = true
                            };
                            Process.Start(psi);
                            allowExit = true;
                            Application.Exit();
                        }
                        catch { }
                    }
                };
            }

            cardRemoteConnect.Controls.Add(lblRemoteTitle);
            cardRemoteConnect.Controls.Add(inputContainerId);
            cardRemoteConnect.Controls.Add(btnConnect);
            cardRemoteConnect.Controls.Add(lblPskLabel);
            cardRemoteConnect.Controls.Add(inputContainerPsk);
            cardRemoteConnect.Controls.Add(btnTopAdmin);

            cardHistory = new ModernCardPanel
            {
                Size = new Size(930, 200),
                Location = new Point(24, 390),
                BackColor = ColorCardBg,
                BorderRadius = 12
            };

            Label lblHistHeader = new Label
            {
                Text = AppI18n.T("PUESTOS DE TRABAJO RECIENTES (HISTORIAL DE SESIONES)", "RECENT WORKSTATIONS (SESSION HISTORY)"),
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = ColorTextDark,
                Location = new Point(24, 14),
                AutoSize = true
            };

            Button btnClearHist = new Button
            {
                Text = AppI18n.T("Limpiar Todo", "Clear All"),
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
                if (MessageBox.Show(AppI18n.T("Â¿Desea borrar todo el historial de conexiones recientes?", "Clear all recent connection history?"), "Connecting", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
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

            panelContentDashboard.Resize += (s, e) => { ResizeDashboardCards(); };
            panelContentSettings.Resize += (s, e) => { ResizeDashboardCards(); };

            RefreshHistoryGrid();
            ResizeDashboardCards();
        }

        private void ResizeDashboardCards()
        {
            try
            {
                int cardWidth = Math.Max(700, panelContentDashboard.Width - 48);

                if (cardLocalToken != null) cardLocalToken.Width = cardWidth;
                if (cardRemoteConnect != null) cardRemoteConnect.Width = cardWidth;
                if (cardHistory != null) cardHistory.Width = cardWidth;
                if (cardSec != null) cardSec.Width = cardWidth;
                if (cardService != null) cardService.Width = cardWidth;

                if (cardLocalToken != null)
                {
                    if (btnCopyId != null) btnCopyId.Location = new Point(cardLocalToken.Width - 320, 75);
                    if (btnRegenerateId != null) btnRegenerateId.Location = new Point(cardLocalToken.Width - 170, 75);
                }
            }
            catch { }
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
                    Text = AppI18n.T("No hay conexiones recientes aÃºn.", "No recent connections yet."),
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
                Label lblAliasHost = new Label { Text = displayTitle, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = ColorTextDark, Location = new Point(8, 8), Size = new Size(135, 22), AutoEllipsis = true };
                Label lblId = new Label { Text = string.Format("ID: {0}", item.Id), Font = new Font("Segoe UI", 8.5F), ForeColor = ColorTextMuted, Location = new Point(8, 30), AutoSize = true };

                Button btnEditAlias = new Button { Text = "âœ", Location = new Point(148, 6), Size = new Size(26, 24), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
                btnEditAlias.FlatAppearance.BorderSize = 0;

                Button btnDelete = new Button { Text = "âœ•", Location = new Point(176, 6), Size = new Size(26, 24), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, ForeColor = Color.FromArgb(225, 29, 72) };
                btnDelete.FlatAppearance.BorderSize = 0;

                string currentTargetId = item.Id;
                string currentAlias = item.Alias;

                btnEditAlias.Click += (s, e) =>
                {
                    string input = PromptInput(AppI18n.T("Asignar Alias", "Set Alias"), string.Format(AppI18n.T("Introduzca un alias para el puesto ID ({0}):", "Enter alias for ID ({0}):"), currentTargetId), currentAlias);
                    if (input != null)
                    {
                        ConnectionHistoryManager.UpdateAlias(currentTargetId, input);
                        RefreshHistoryGrid();
                    }
                };

                btnDelete.Click += (s, e) =>
                {
                    if (MessageBox.Show(string.Format(AppI18n.T("Â¿Eliminar puesto ID ({0}) del historial?", "Delete ID ({0}) from history?"), currentTargetId), "Connecting", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        ConnectionHistoryManager.RemoveSession(currentTargetId);
                        RefreshHistoryGrid();
                    }
                };

                ModernButton btnQuickConn = new ModernButton
                {
                    Text = AppI18n.T("Conectar", "Connect"),
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
            Button confirmation = new Button() { Text = AppI18n.T("Guardar", "Save"), Left = 260, Width = 120, Top = 95, DialogResult = DialogResult.OK, Height = 34, BackColor = ColorCyanPrimary, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(textLabel);
            prompt.AcceptButton = confirmation;

            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : null;
        }

        private void BuildSettingsTab()
        {
            cardSec = new ModernCardPanel { Size = new Size(930, 380), Location = new Point(24, 20), BackColor = ColorCardBg, BorderRadius = 12, Padding = new Padding(24) };
            Label lblSecHeader = new Label { Text = AppI18n.T("ConfiguraciÃ³n Global de Seguridad y Acceso Desatendido", "Global Security & Unattended Access Settings"), Font = new Font("Segoe UI", 13F, FontStyle.Bold), Location = new Point(24, 20), AutoSize = true, ForeColor = ColorTextDark };

            chkUnattendedAccess = new CheckBox { Text = AppI18n.T("Permitir Acceso Desatendido directo con Clave PSK (sin confirmaciÃ³n)", "Allow direct unattended access with PSK Key (no prompt)"), Checked = true, Location = new Point(24, 65), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            
            Label lblCustomPskLabel = new Label { Text = AppI18n.T("Clave de Acceso Desatendido Personalizada:", "Custom Unattended Access Key:"), Location = new Point(24, 105), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            
            txtCustomPsk = new TextBox
            {
                Location = new Point(370, 102),
                Size = new Size(180, 28),
                Font = new Font("Segoe UI", 10.5F),
                UseSystemPasswordChar = true,
                Text = PeerResolver.GetCustomPsk()
            };
            txtCustomPsk.TextChanged += (s, e) => { PeerResolver.SaveCustomPsk(txtCustomPsk.Text); };

            CheckBox chkShowCustomPsk = new CheckBox
            {
                Text = AppI18n.T("Mostrar Clave", "Show Key"),
                Location = new Point(560, 105),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F)
            };
            chkShowCustomPsk.CheckedChanged += (s, e) => { txtCustomPsk.UseSystemPasswordChar = !chkShowCustomPsk.Checked; };

            Label lblUserAliasLabel = new Label { Text = AppI18n.T("Nombre de PresentaciÃ³n (Alias en Chat y ConexiÃ³n):", "Display Name (Chat & Connection Alias):"), Location = new Point(24, 150), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            TextBox txtUserAlias = new TextBox
            {
                Location = new Point(370, 147),
                Size = new Size(180, 28),
                Font = new Font("Segoe UI", 10F),
                Text = PeerResolver.GetUserDisplayName()
            };
            txtUserAlias.TextChanged += (s, e) => { PeerResolver.SaveUserDisplayName(txtUserAlias.Text); };

            CheckBox c2 = new CheckBox { Text = AppI18n.T("Aislamiento total de teclado sin interferencias locales", "Total keyboard isolation without local interference"), Checked = true, Location = new Point(24, 190), AutoSize = true, Font = new Font("Segoe UI", 10F) };
            CheckBox c3 = new CheckBox { Text = AppI18n.T("Acceder a portapapeles bidireccional en tiempo real", "Real-time bidirectional clipboard access"), Checked = true, Location = new Point(24, 225), AutoSize = true, Font = new Font("Segoe UI", 10F) };
            CheckBox c4 = new CheckBox { Text = AppI18n.T("Minimizar a la barra de tareas (Segundo Plano al presionar Cerrar X)", "Minimize to system tray on Close (X)"), Checked = true, Location = new Point(24, 260), AutoSize = true, Font = new Font("Segoe UI", 10F) };
            CheckBox cAudio = new CheckBox { Text = AppI18n.T("Transmitir Audio del Equipo Remoto (Desactivado por defecto)", "Stream Remote Computer Audio (Disabled by default)"), Checked = false, Location = new Point(24, 295), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = ColorTextMuted };

            cardSec.Controls.Add(lblSecHeader);
            cardSec.Controls.Add(chkUnattendedAccess);
            cardSec.Controls.Add(lblCustomPskLabel);
            cardSec.Controls.Add(txtCustomPsk);
            cardSec.Controls.Add(chkShowCustomPsk);
            cardSec.Controls.Add(lblUserAliasLabel);
            cardSec.Controls.Add(txtUserAlias);
            cardSec.Controls.Add(c2);
            cardSec.Controls.Add(c3);
            cardSec.Controls.Add(c4);
            cardSec.Controls.Add(cAudio);

            cardService = new ModernCardPanel { Size = new Size(930, 190), Location = new Point(24, 415), BackColor = ColorCardBg, BorderRadius = 12, Padding = new Padding(24) };
            Label lblSvcHeader = new Label { Text = AppI18n.T("Servicio de Asistencia de Windows, Idioma y Relay Server", "Windows Assistance Service, Language & Relay Server"), Font = new Font("Segoe UI", 12F, FontStyle.Bold), Location = new Point(24, 16), AutoSize = true, ForeColor = ColorTextDark };
            
            Label lblLang = new Label { Text = AppI18n.T("Idioma de la AplicaciÃ³n:", "Application Language:"), Location = new Point(24, 55), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            ComboBox cboLang = new ComboBox { Location = new Point(190, 52), Size = new Size(150, 28), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 10F) };
            cboLang.Items.Add("EspaÃ±ol (ES)");
            cboLang.Items.Add("English (EN)");
            
            string savedLang = PeerResolver.GetSavedLanguage();
            cboLang.SelectedIndex = (savedLang == "en") ? 1 : 0;
            cboLang.SelectedIndexChanged += (s, e) =>
            {
                string sel = (cboLang.SelectedIndex == 1) ? "en" : "es";
                if (sel != PeerResolver.GetSavedLanguage())
                {
                    PeerResolver.SaveLanguage(sel);
                    MessageBox.Show(AppI18n.T("La aplicaciÃ³n se reiniciarÃ¡ para aplicar los cambios de idioma.", "The application will restart to apply language changes."), "Connecting", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Application.Restart();
                }
            };

            Label lblRelayHostLabel = new Label { Text = AppI18n.T("Servidor Relay Personalizado (Dominio o IP):", "Custom Relay Server (Domain or IP):"), Location = new Point(24, 98), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            TextBox txtRelayHost = new TextBox
            {
                Location = new Point(370, 95),
                Size = new Size(240, 28),
                Font = new Font("Segoe UI", 10F),
                Text = PeerResolver.GetCustomRelayHost()
            };
            txtRelayHost.TextChanged += (s, e) => { PeerResolver.SaveCustomRelayHost(txtRelayHost.Text); };

            bool isSvcInstalled = PeerResolver.IsWindowsServiceInstalled("ConnectingService");
            ModernButton btnInstallSvc = new ModernButton
            {
                Text = isSvcInstalled ? AppI18n.T("Desinstalar Servicio de Windows", "Uninstall Windows Service") : AppI18n.T("Instalar Servicio de Windows", "Install Windows Service"),
                Location = new Point(360, 48),
                Size = new Size(250, 38),
                NormalColor = isSvcInstalled ? Color.FromArgb(225, 29, 72) : ColorCyanPrimary,
                HoverColor = isSvcInstalled ? Color.FromArgb(190, 18, 60) : ColorCyanDark,
                BorderRadius = 8
            };
            btnInstallSvc.Click += (s, e) =>
            {
                bool currentlyInstalled = PeerResolver.IsWindowsServiceInstalled("ConnectingService");
                if (currentlyInstalled)
                {
                    if (MessageBox.Show(AppI18n.T("Â¿Desea detener y desinstalar el Servicio de Windows ConnectingService?", "Stop and uninstall ConnectingService Windows Service?"), "Connecting", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        try
                        {
                            ProcessStartInfo psi = new ProcessStartInfo
                            {
                                FileName = "cmd.exe",
                                Arguments = "/c sc stop \"ConnectingService\" & sc delete \"ConnectingService\"",
                                Verb = "runas",
                                UseShellExecute = true
                            };
                            Process p = Process.Start(psi);
                            p.WaitForExit();
                            MessageBox.Show(AppI18n.T("Servicio desinstalado correctamente.", "Service uninstalled successfully."), "Connecting", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            btnInstallSvc.Text = AppI18n.T("Instalar Servicio de Windows", "Install Windows Service");
                            btnInstallSvc.NormalColor = ColorCyanPrimary;
                            btnInstallSvc.HoverColor = ColorCyanDark;
                            btnInstallSvc.Invalidate();
                        }
                        catch (Exception ex) { MessageBox.Show("Error: " + ex.Message, "Connecting", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                    }
                }
                else
                {
                    if (MessageBox.Show(AppI18n.T("Â¿Desea crear e iniciar el Servicio de Windows ConnectingService?", "Create and start ConnectingService Windows Service?"), "Connecting", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        try
                        {
                            ProcessStartInfo psi = new ProcessStartInfo
                            {
                                FileName = "cmd.exe",
                                Arguments = "/c sc stop \"ConnectingService\" 2>nul & sc delete \"ConnectingService\" 2>nul & sc create \"ConnectingService\" binPath= \"" + Application.ExecutablePath + " --service\" start= auto & sc start \"ConnectingService\"",
                                Verb = "runas",
                                UseShellExecute = true
                            };
                            Process p = Process.Start(psi);
                            p.WaitForExit();
                            MessageBox.Show(AppI18n.T("Servicio instalado e iniciado correctamente.", "Service installed and started successfully."), "Connecting", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            btnInstallSvc.Text = AppI18n.T("Desinstalar Servicio de Windows", "Uninstall Windows Service");
                            btnInstallSvc.NormalColor = Color.FromArgb(225, 29, 72);
                            btnInstallSvc.HoverColor = Color.FromArgb(190, 18, 60);
                            btnInstallSvc.Invalidate();
                        }
                        catch (Exception ex) { MessageBox.Show("Error: " + ex.Message, "Connecting", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                    }
                }
            };

            cardService.Controls.Add(lblSvcHeader);
            cardService.Controls.Add(lblLang);
            cardService.Controls.Add(cboLang);
            cardService.Controls.Add(lblRelayHostLabel);
            cardService.Controls.Add(txtRelayHost);
            cardService.Controls.Add(btnInstallSvc);

            panelContentSettings.Controls.Add(cardSec);
            panelContentSettings.Controls.Add(cardService);
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
                MessageBox.Show(AppI18n.T("Por favor introduzca la ID remota de 9 dÃ­gitos.", "Please enter the 9-digit remote ID."), "Connecting", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string pskInput = txtRemotePsk.Text.Trim();
            if (string.IsNullOrEmpty(pskInput))
            {
                MessageBox.Show(AppI18n.T("La Clave PSK es OBLIGATORIA para iniciar una sesiÃ³n remota.", "PSK Key is REQUIRED to start a remote session."), "Connecting", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string cleanCheck = PeerResolver.ExtractRawDigitsId(rawInput);
            if (cleanCheck == rawNumId)
            {
                MessageBox.Show(AppI18n.T("No se puede conectar a su propia ID.", "Cannot connect to your own ID."), "Connecting", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ConnectingProgressForm progressForm = new ConnectingProgressForm(rawInput);

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

                    this.Invoke((MethodInvoker)delegate
                    {
                        progressForm.Close();
                        RemoteSessionView sessionView = new RemoteSessionView(rawInput, remoteHostname, pskInput, rawNumId, client);
                        sessionTabControl.AddSessionTab(sessionView);
                        RefreshHistoryGrid();
                    });
                }
                catch (Exception ex)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        progressForm.Close();
                        MessageBox.Show("Error: " + ex.Message, "Connecting", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    });
                }
            }) { IsBackground = true };

            connThread.Start();
            progressForm.ShowDialog();
        }
    }
}


namespace Conecting.UI
{
    /// <summary>
    /// Custom Premium Rounded Button Control.
    /// Uses native Win32 Region clipping to guarantee rounded pill edges without square hover artifacts.
    /// </summary>
    public class ModernButton : Control
    {
        public int BorderRadius { get; set; }
        public Color HoverColor { get; set; }
        public Color NormalColor { get; set; }

        private bool isHovered = false;

        public ModernButton()
        {
            this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BorderRadius = 8;
            this.NormalColor = Color.FromArgb(14, 98, 115);
            this.HoverColor = Color.FromArgb(8, 70, 84);
            this.BackColor = Color.Transparent;
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            this.Cursor = Cursors.Hand;
            this.DoubleBuffered = true;

            this.MouseEnter += (s, e) => { isHovered = true; this.Invalidate(); };
            this.MouseLeave += (s, e) => { isHovered = false; this.Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            if (this.Parent != null)
            {
                using (SolidBrush parentBrush = new SolidBrush(this.Parent.BackColor))
                {
                    g.FillRectangle(parentBrush, this.ClientRectangle);
                }
            }

            Color currentBg = isHovered ? HoverColor : NormalColor;
            int radius = Math.Min(BorderRadius, Math.Min(this.Width, this.Height));
            if (radius > 0)
            {
                using (GraphicsPath path = GetRoundedPath(new Rectangle(0, 0, this.Width - 1, this.Height - 1), radius))
                {
                    using (SolidBrush brush = new SolidBrush(currentBg))
                    {
                        g.FillPath(brush, path);
                    }
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

    /// <summary>
    /// Rounded Card Container Panel with subtle border.
    /// </summary>
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

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateRegion();
        }

        private void UpdateRegion()
        {
            try
            {
                if (this.Width > 0 && this.Height > 0)
                {
                    using (GraphicsPath path = GetRoundedPath(new Rectangle(0, 0, this.Width, this.Height), BorderRadius))
                    {
                        this.Region = new Region(path);
                    }
                }
            }
            catch { }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = GetRoundedPath(new Rectangle(0, 0, this.Width - 1, this.Height - 1), BorderRadius))
            {
                using (Pen pen = new Pen(BorderColor, 1.5f))
                {
                    e.Graphics.DrawPath(pen, path);
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

    /// <summary>
    /// Rounded Input Container for TextBoxes.
    /// </summary>
    public class ModernInputContainer : Panel
    {
        public int BorderRadius { get; set; }
        public Color BorderColor { get; set; }

        public ModernInputContainer()
        {
            this.BorderRadius = 8;
            this.BorderColor = Color.FromArgb(203, 213, 225);
            this.BackColor = Color.White;
            this.Padding = new Padding(12, 10, 12, 10);
            this.DoubleBuffered = true;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateRegion();
        }

        private void UpdateRegion()
        {
            try
            {
                if (this.Width > 0 && this.Height > 0)
                {
                    using (GraphicsPath path = GetRoundedPath(new Rectangle(0, 0, this.Width, this.Height), BorderRadius))
                    {
                        this.Region = new Region(path);
                    }
                }
            }
            catch { }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = GetRoundedPath(new Rectangle(0, 0, this.Width - 1, this.Height - 1), BorderRadius))
            {
                using (Pen pen = new Pen(BorderColor, 1.5f))
                {
                    e.Graphics.DrawPath(pen, path);
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

    /// <summary>
    /// Double-buffered PictureBox for flicker-free remote frame rendering.
    /// </summary>
    public class SmoothPictureBox : PictureBox
    {
        public SmoothPictureBox()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            pe.Graphics.InterpolationMode = InterpolationMode.Bilinear;
            pe.Graphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;
            pe.Graphics.SmoothingMode = SmoothingMode.HighSpeed;
            base.OnPaint(pe);
        }
    }
}


namespace Conecting.UI
{
    /// <summary>
    /// Remote Session Viewer Control embedded within a Session Tab.
    /// Manages real-time desktop frame reception, input dispatching, and 45-second auto reconnection.
    /// </summary>
    public class RemoteSessionView : UserControl
    {
        private SmoothPictureBox picRemoteDesktop;
        private Panel panelChatDrawer;
        private Panel panelTtyDrawer;
        private Panel overlayReconnecting;
        private Label lblReconnectingText;
        private RichTextBox txtChatHistory;
        private RichTextBox txtTtyHistory;
        private TextBox txtChatMessage;
        private TextBox txtTtyInput;
        private ModernButton btnSendChat;
        private ModernButton btnSendTty;
        private ModernButton btnCloseChatDrawer;
        private ModernButton btnCloseTtyDrawer;
        private ModernButton btnDisconnect;
        private ModernButton btnChat;
        private ModernButton btnTtyConsole;
        private ModernButton btnQuickActions;
        private ContextMenuStrip menuQuickActions;
        private ContextMenuStrip menuView;
        private ContextMenuStrip menuMainMenu;

        private TcpClient client;
        private NetworkStream stream;
        private bool isSessionActive = true;
        private Thread receiveThread;
        private Thread clipboardThread;
        private string lastClipboardText = "";

        public string TargetId { get; private set; }
        public string Hostname { get; private set; }
        private string remotePskKey;
        private string myNodeId;
        public Action OnCloseSessionRequested { get; set; }

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

        public RemoteSessionView(string remoteId, string remoteHostname, string remotePskKey, string myNodeId, TcpClient client)
        {
            this.TargetId = remoteId;
            this.Hostname = string.IsNullOrEmpty(remoteHostname) ? "PC-REMOTO" : remoteHostname;
            this.remotePskKey = remotePskKey;
            this.myNodeId = myNodeId;
            this.client = client;
            this.client.NoDelay = true;
            this.stream = client.GetStream();

            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(245, 247, 250);

            InitializeUI();
            StartThreads();
        }

        public FlowLayoutPanel SessionActionsPanel { get; private set; }

        private void InitializeUI()
        {
            SessionActionsPanel = new FlowLayoutPanel
            {
                Height = 34,
                AutoSize = true,
                WrapContents = false,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 10, 0)
            };

            btnDisconnect = new ModernButton
            {
                Text = AppI18n.T("Finalizar", "End"),
                Size = new Size(75, 32),
                NormalColor = Color.FromArgb(239, 68, 68),
                HoverColor = Color.FromArgb(220, 38, 38),
                BorderRadius = 5,
                Margin = new Padding(4, 0, 0, 0),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            btnDisconnect.Click += (s, e) => { CloseSession(); };

            btnChat = new ModernButton
            {
                Text = "Chat",
                Size = new Size(55, 32),
                NormalColor = Color.FromArgb(241, 245, 249),
                HoverColor = Color.FromArgb(226, 232, 240),
                ForeColor = Color.FromArgb(15, 23, 42),
                BorderRadius = 5,
                Margin = new Padding(4, 0, 0, 0),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };

            btnTtyConsole = new ModernButton
            {
                Text = "TTY",
                Size = new Size(55, 32),
                NormalColor = Color.FromArgb(15, 23, 42),
                HoverColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.FromArgb(74, 222, 128),
                BorderRadius = 5,
                Margin = new Padding(4, 0, 0, 0),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Visible = false
            };

            btnQuickActions = new ModernButton
            {
                Text = AppI18n.T("Acciones", "Actions"),
                Size = new Size(75, 32),
                NormalColor = Color.FromArgb(241, 245, 249),
                HoverColor = Color.FromArgb(226, 232, 240),
                ForeColor = Color.FromArgb(15, 23, 42),
                BorderRadius = 5,
                Margin = new Padding(4, 0, 0, 0),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            BuildQuickActionsMenu();
            btnQuickActions.Click += (s, e) => { menuQuickActions.Show(btnQuickActions, new Point(0, btnQuickActions.Height)); };

            ModernButton btnViewMode = new ModernButton
            {
                Text = AppI18n.T("Vista", "View"),
                Size = new Size(60, 32),
                NormalColor = Color.FromArgb(241, 245, 249),
                HoverColor = Color.FromArgb(226, 232, 240),
                ForeColor = Color.FromArgb(15, 23, 42),
                BorderRadius = 5,
                Margin = new Padding(4, 0, 0, 0),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            BuildViewMenu(btnViewMode);

            ModernButton btnMainMenu = new ModernButton
            {
                Text = AppI18n.T("MenÃº", "Menu"),
                Size = new Size(60, 32),
                NormalColor = Color.FromArgb(241, 245, 249),
                HoverColor = Color.FromArgb(226, 232, 240),
                ForeColor = Color.FromArgb(15, 23, 42),
                BorderRadius = 5,
                Margin = new Padding(4, 0, 0, 0),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            BuildMainMenu(btnMainMenu);

            SessionActionsPanel.Controls.Add(btnMainMenu);
            SessionActionsPanel.Controls.Add(btnViewMode);
            SessionActionsPanel.Controls.Add(btnQuickActions);
            SessionActionsPanel.Controls.Add(btnTtyConsole);
            SessionActionsPanel.Controls.Add(btnChat);
            SessionActionsPanel.Controls.Add(btnDisconnect);

            BuildChatDrawer();
            BuildTtyDrawer();

            overlayReconnecting = new Panel
            {
                Size = new Size(440, 60),
                BackColor = Color.FromArgb(239, 68, 68),
                Visible = false
            };
            lblReconnectingText = new Label
            {
                Text = AppI18n.T("Reconectando sesiÃ³n en tiempo real...\nRestableciendo enlace con el host remoto.", "Reconnecting session in real time...\nRestoring link with remote host."),
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

            this.Controls.Add(picRemoteDesktop);
            this.Controls.Add(panelChatDrawer);
            panelChatDrawer.BringToFront();
        }

        private int currentQualityLevel = 75;

        private void BuildViewMenu(Control btnTarget)
        {
            menuView = new ContextMenuStrip();
            ToolStripMenuItem itemStretch = new ToolStripMenuItem(AppI18n.T("Ajustar a la Ventana", "Fit to Window"));
            ToolStripMenuItem itemOriginal = new ToolStripMenuItem(AppI18n.T("TamaÃ±o Original (1:1)", "Original Size (1:1)"));
            ToolStripMenuItem itemFull = new ToolStripMenuItem(AppI18n.T("Pantalla Completa (F11)", "Full Screen (F11)"));

            itemStretch.Click += (s, e) => { picRemoteDesktop.SizeMode = PictureBoxSizeMode.Zoom; };
            itemOriginal.Click += (s, e) => { picRemoteDesktop.SizeMode = PictureBoxSizeMode.CenterImage; };
            itemFull.Click += (s, e) =>
            {
                Form p = this.FindForm();
                if (p != null) { p.WindowState = (p.WindowState == FormWindowState.Maximized) ? FormWindowState.Normal : FormWindowState.Maximized; }
            };

            menuView.Items.Add(itemStretch);
            menuView.Items.Add(itemOriginal);
            menuView.Items.Add(itemFull);
            menuView.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem menuQualityHeader = new ToolStripMenuItem(AppI18n.T("Calidad de TransmisiÃ³n:", "Transmission Quality:")) { Enabled = false };
            ToolStripMenuItem itemQualityBalanced = new ToolStripMenuItem(AppI18n.T("Balanceado (Recomendado)", "Balanced (Recommended)"));
            ToolStripMenuItem itemQualityBest = new ToolStripMenuItem(AppI18n.T("Mejor Aspecto (Alta DefiniciÃ³n)", "Best Quality (High Definition)"));
            ToolStripMenuItem itemQualityFast = new ToolStripMenuItem(AppI18n.T("RÃ¡pida (Baja Latencia)", "Fast (Low Latency)"));

            Action updateQualityChecks = () =>
            {
                itemQualityBalanced.Text = (currentQualityLevel == 75 ? "âœ“ " : "  ") + AppI18n.T("Balanceado (Recomendado)", "Balanced (Recommended)");
                itemQualityBest.Text = (currentQualityLevel == 90 ? "âœ“ " : "  ") + AppI18n.T("Mejor Aspecto (Alta DefiniciÃ³n)", "Best Quality (High Definition)");
                itemQualityFast.Text = (currentQualityLevel == 60 ? "âœ“ " : "  ") + AppI18n.T("RÃ¡pida (Baja Latencia)", "Fast (Low Latency)");
            };

            itemQualityBalanced.Click += (s, e) =>
            {
                currentQualityLevel = 75;
                updateQualityChecks();
                SendQualityCommand(75);
            };

            itemQualityBest.Click += (s, e) =>
            {
                currentQualityLevel = 90;
                updateQualityChecks();
                SendQualityCommand(90);
            };

            itemQualityFast.Click += (s, e) =>
            {
                currentQualityLevel = 60;
                updateQualityChecks();
                SendQualityCommand(60);
            };

            updateQualityChecks();

            menuView.Items.Add(menuQualityHeader);
            menuView.Items.Add(itemQualityBalanced);
            menuView.Items.Add(itemQualityBest);
            menuView.Items.Add(itemQualityFast);

            btnTarget.Click += (s, e) => { menuView.Show(btnTarget, new Point(0, btnTarget.Height)); };
        }

        private void SendQualityCommand(int quality)
        {
            try
            {
                if (stream != null)
                {
                    byte[] data = Encoding.UTF8.GetBytes(quality.ToString());
                    PacketProtocol.SendPacket(stream, 0x05, data);
                }
            }
            catch { }
        }

        private void BuildMainMenu(Control btnTarget)
        {
            menuMainMenu = new ContextMenuStrip();
            menuMainMenu.Items.Add(AppI18n.T("Sobre Connecting...", "About Connecting..."), null, (s, e) =>
            {
                using (AboutForm about = new AboutForm()) { about.ShowDialog(); }
            });
            menuMainMenu.Items.Add(AppI18n.T("Ayuda y DocumentaciÃ³n", "Help & Documentation"), null, (s, e) =>
            {
                try { System.Diagnostics.Process.Start("https://jh4n3r.github.io/connecting/docs/"); } catch { }
            });
            menuMainMenu.Items.Add("-");
            menuMainMenu.Items.Add(AppI18n.T("Finalizar SesiÃ³n Remota", "End Remote Session"), null, (s, e) => { CloseSession(); });
            btnTarget.Click += (s, e) => { menuMainMenu.Show(btnTarget, new Point(0, btnTarget.Height)); };
        }

        private void BuildQuickActionsMenu()
        {
            menuQuickActions = new ContextMenuStrip();
            menuQuickActions.Items.Add("Enviar Ctrl + Alt + Supr", null, (s, e) => { SendKeyCombo(VK_CONTROL, VK_MENU, VK_DELETE); });
            menuQuickActions.Items.Add(AppI18n.T("Administrador de Tareas", "Task Manager"), null, (s, e) => { SendKeyCombo(VK_CONTROL, 0x10, 0x1B); });
            menuQuickActions.Items.Add(AppI18n.T("Mostrar Escritorio", "Show Desktop"), null, (s, e) => { SendKeyCombo(VK_LWIN, (byte)'D'); });
            menuQuickActions.Items.Add(AppI18n.T("Explorador de Archivos", "File Explorer"), null, (s, e) => { SendKeyCombo(VK_LWIN, (byte)'E'); });
            menuQuickActions.Items.Add(AppI18n.T("Bloquear Equipo Remoto", "Lock Remote Computer"), null, (s, e) => { SendKeyCombo(VK_LWIN, (byte)'L'); });
        }

        private void BuildChatDrawer()
        {
            panelChatDrawer = new Panel
            {
                Size = new Size(300, 440),
                BackColor = Color.White,
                Visible = false,
                Padding = new Padding(12),
                BorderStyle = BorderStyle.FixedSingle
            };

            Label lblChatHeader = new Label { Text = "ðŸ’¬ Chat de SesiÃ³n Remota", Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), Location = new Point(12, 12), AutoSize = true, ForeColor = Color.FromArgb(14, 98, 115) };
            btnCloseChatDrawer = new ModernButton { Text = "âœ•", Location = new Point(260, 8), Size = new Size(28, 28), NormalColor = Color.FromArgb(239, 68, 68), HoverColor = Color.FromArgb(220, 38, 38), BorderRadius = 4 };
            btnCloseChatDrawer.Click += (s, e) => { panelChatDrawer.Visible = false; };

            txtChatHistory = new RichTextBox { Location = new Point(12, 44), Size = new Size(276, 345), ReadOnly = true, BackColor = Color.FromArgb(248, 250, 252), BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 9.5F) };
            txtChatMessage = new TextBox { Location = new Point(12, 402), Size = new Size(205, 30), Font = new Font("Segoe UI", 10F) };
            txtChatMessage.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SendChatMessage(); } };

            btnSendChat = new ModernButton { Text = "Enviar", Location = new Point(223, 400), Size = new Size(65, 32), NormalColor = Color.FromArgb(14, 98, 115), HoverColor = Color.FromArgb(8, 70, 84), BorderRadius = 6 };
            btnSendChat.Click += (s, e) => { SendChatMessage(); };

            panelChatDrawer.Controls.Add(lblChatHeader);
            panelChatDrawer.Controls.Add(btnCloseChatDrawer);
            panelChatDrawer.Controls.Add(txtChatHistory);
            panelChatDrawer.Controls.Add(txtChatMessage);
            panelChatDrawer.Controls.Add(btnSendChat);

            btnChat.Click += (s, e) =>
            {
                panelChatDrawer.Location = new Point(this.ClientSize.Width - 320, 10);
                panelChatDrawer.Visible = !panelChatDrawer.Visible;
                if (panelChatDrawer.Visible) { panelChatDrawer.BringToFront(); txtChatMessage.Focus(); }
            };
        }

        private void BuildTtyDrawer()
        {
            panelTtyDrawer = new Panel { Size = new Size(440, 480), BackColor = Color.FromArgb(15, 23, 42), Visible = false, Padding = new Padding(12), BorderStyle = BorderStyle.FixedSingle };
            Label lblTtyHeader = new Label { Text = "ðŸ’» Consola Interactiva TTY", Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), Location = new Point(12, 12), AutoSize = true, ForeColor = Color.FromArgb(0, 172, 193) };
            btnCloseTtyDrawer = new ModernButton { Text = "âœ•", Location = new Point(400, 8), Size = new Size(28, 28), NormalColor = Color.FromArgb(239, 68, 68), HoverColor = Color.FromArgb(220, 38, 38), BorderRadius = 4 };
            btnCloseTtyDrawer.Click += (s, e) => { panelTtyDrawer.Visible = false; };

            txtTtyHistory = new RichTextBox { Location = new Point(12, 44), Size = new Size(416, 375), ReadOnly = true, BackColor = Color.FromArgb(2, 6, 23), ForeColor = Color.FromArgb(74, 222, 128), BorderStyle = BorderStyle.None, Font = new Font("Consolas", 9.5F) };
            txtTtyInput = new TextBox { Location = new Point(12, 432), Size = new Size(340, 30), Font = new Font("Consolas", 10F), BackColor = Color.FromArgb(15, 23, 42), ForeColor = Color.White };
            txtTtyInput.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SendTtyInput(); } };
            btnSendTty = new ModernButton { Text = "Enviar", Location = new Point(363, 430), Size = new Size(65, 32), NormalColor = Color.FromArgb(0, 172, 193), HoverColor = Color.FromArgb(0, 131, 143), BorderRadius = 6 };
            btnSendTty.Click += (s, e) => { SendTtyInput(); };

            panelTtyDrawer.Controls.Add(lblTtyHeader);
            panelTtyDrawer.Controls.Add(btnCloseTtyDrawer);
            panelTtyDrawer.Controls.Add(txtTtyHistory);
            panelTtyDrawer.Controls.Add(txtTtyInput);
            panelTtyDrawer.Controls.Add(btnSendTty);

            btnTtyConsole.Click += (s, e) =>
            {
                panelTtyDrawer.Location = new Point(this.ClientSize.Width - 460, 10);
                panelTtyDrawer.Visible = !panelTtyDrawer.Visible;
                if (panelTtyDrawer.Visible) { panelTtyDrawer.BringToFront(); txtTtyInput.Focus(); }
            };
        }

        private void StartThreads()
        {
            receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
            receiveThread.Start();
            StartClipboardSyncThread();
            InstallKeyboardHook();
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
                        }
                        _isRenderingFrame = false;
                    });
                }
            }
            catch { }
        }

        private IntPtr parentFormHandle = IntPtr.Zero;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                Form f = this.FindForm();
                if (f != null) parentFormHandle = f.Handle;
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

        private void PicRemoteDesktop_MouseDown(object sender, MouseEventArgs e)
        {
            try
            {
                this.Focus();
                picRemoteDesktop.Focus();
            }
            catch { }
            byte evtType = (e.Button == MouseButtons.Right) ? (byte)0x04 : (byte)0x02;
            SendRemoteInput(e.X, e.Y, evtType);
        }

        private long _lastMouseMoveTick = 0;
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
            using (System.Diagnostics.Process curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (System.Diagnostics.ProcessModule curModule = curProcess.MainModule)
            {
                _keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                IntPtr activeWin = GetForegroundWindow();
                IntPtr targetWin = (parentFormHandle != IntPtr.Zero) ? parentFormHandle : (this.ParentForm != null ? this.ParentForm.Handle : IntPtr.Zero);

                if (nCode >= 0 && targetWin != IntPtr.Zero && activeWin == targetWin && !txtChatMessage.Focused)
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
            }
            catch { }
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

        private void SendTtyInput()
        {
            string cmd = txtTtyInput.Text;
            if (string.IsNullOrEmpty(cmd)) return;
            txtTtyInput.Clear();
            txtTtyHistory.AppendText("$ " + cmd + "\n");
            txtTtyHistory.ScrollToCaret();

            byte[] bytes = Encoding.UTF8.GetBytes(cmd + "\n");
            PacketProtocol.SendPacket(stream, 0x07, bytes);
        }

        private void ShowReconnectingOverlay(bool visible)
        {
            if (this.IsDisposed) return;
            try
            {
                this.Invoke((MethodInvoker)delegate
                {
                    if (overlayReconnecting != null && !overlayReconnecting.IsDisposed)
                    {
                        overlayReconnecting.Visible = visible;
                        overlayReconnecting.BringToFront();
                    }
                });
            }
            catch { }
        }

        private bool TryAutoReconnect()
        {
            ShowReconnectingOverlay(true);
            
            for (int attempt = 1; attempt <= 15; attempt++)
            {
                if (!isSessionActive) return false;
                
                try
                {
                    if (this.client != null) { try { this.client.Close(); } catch { } }
                    
                    string errorMsg;
                    string newHostname;
                    TcpClient newClient = PeerResolver.DiscoverAndConnectPeer(TargetId, myNodeId, remotePskKey, out newHostname, out errorMsg);
                    
                    if (newClient != null && newClient.Connected)
                    {
                        this.client = newClient;
                        this.client.NoDelay = true;
                        this.stream = newClient.GetStream();
                        ShowReconnectingOverlay(false);
                        return true;
                    }
                }
                catch { }

                Thread.Sleep(3000);
            }

            ShowReconnectingOverlay(false);
            return false;
        }

        private void ReceiveLoop()
        {
            while (isSessionActive)
            {
                try
                {
                    if (client == null || !client.Connected)
                    {
                        if (!TryAutoReconnect())
                        {
                            NotifyHostClosed();
                            break;
                        }
                        continue;
                    }

                    byte pktType;
                    byte[] payload;
                    if (!PacketProtocol.ReadPacket(stream, out pktType, out payload))
                    {
                        if (!TryAutoReconnect())
                        {
                            NotifyHostClosed();
                            break;
                        }
                        continue;
                    }

                    ShowReconnectingOverlay(false);

                    if (pktType == 0x03) // CHAT
                    {
                        string chatStr = Encoding.UTF8.GetString(payload);
                        this.Invoke((MethodInvoker)delegate
                        {
                            panelChatDrawer.Location = new Point(this.ClientSize.Width - 320, 10);
                            panelChatDrawer.BringToFront();
                            panelChatDrawer.Visible = true;
                            txtChatHistory.AppendText(chatStr + "\n");
                            txtChatHistory.ScrollToCaret();
                        });
                    }
                    else if (pktType == 0x09) // CAPS
                    {
                        string caps = Encoding.UTF8.GetString(payload);
                        this.Invoke((MethodInvoker)delegate
                        {
                            btnTtyConsole.Visible = caps.Contains("CAPS:LINUX");
                        });
                    }
                    else if (pktType == 0x07) // TTY
                    {
                        string ttyStr = Encoding.UTF8.GetString(payload);
                        this.Invoke((MethodInvoker)delegate
                        {
                            btnTtyConsole.Visible = true;
                            panelTtyDrawer.Location = new Point(this.ClientSize.Width - 460, 10);
                            panelTtyDrawer.BringToFront();
                            panelTtyDrawer.Visible = true;
                            txtTtyHistory.AppendText(ttyStr);
                            txtTtyHistory.ScrollToCaret();
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
                    else if (pktType == 0x00) // FRAME
                    {
                        OnFrameReceived(payload);
                    }
                }
                catch
                {
                    if (!TryAutoReconnect())
                    {
                        NotifyHostClosed();
                        break;
                    }
                }
            }
        }

        private void NotifyHostClosed()
        {
            if (this.IsDisposed) return;
            try
            {
                this.Invoke((MethodInvoker)delegate
                {
                    MessageBox.Show(
                        string.Format(AppI18n.T("El equipo remoto ({0}) ha finalizado la sesiÃ³n.", "Remote computer ({0}) ended the session."), TargetId),
                        "Connecting",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                    CloseSession();
                });
            }
            catch { }
        }

        public void CloseSession()
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
                    byte[] discBytes = Encoding.UTF8.GetBytes("CLIENT_DISCONNECTED\n");
                    PacketProtocol.SendPacket(stream, 0x03, discBytes);
                }
            }
            catch { }

            try { if (client != null) client.Close(); } catch { }

            if (OnCloseSessionRequested != null)
            {
                OnCloseSessionRequested();
            }
        }
    }
}


namespace Conecting.UI
{
    public class SessionTabItem
    {
        public string Title { get; set; }
        public RemoteSessionView SessionView { get; set; }
        public ModernButton TabButton { get; set; }
        public Button CloseButton { get; set; }
    }

    /// <summary>
    /// AnyDesk-Style Unified Multi-Session Navigation & Tab System.
    /// Integrates remote session tabs directly into the main top navigation bar.
    /// </summary>
    public class SessionTabControl : Panel
    {
        private FlowLayoutPanel navTabsFlowPanel;
        private Panel contentContainerPanel;
        private ModernButton btnDashboardTab;
        private ModernButton btnNewTab;
        private List<SessionTabItem> activeTabs = new List<SessionTabItem>();
        private Panel dashboardContentPanel;
        private Panel settingsContentPanel;
        private Panel topHeaderPanel;
        private SessionTabItem currentSelectedTab = null;
        private bool isSettingsViewActive = false;

        public Action OnNewTabClick { get; set; }

        public SessionTabControl(FlowLayoutPanel navFlow, Panel contentContainer, Panel dashboardPanel, Panel settingsPanel, Panel topHeader)
        {
            this.navTabsFlowPanel = navFlow;
            this.contentContainerPanel = contentContainer;
            this.dashboardContentPanel = dashboardPanel;
            this.settingsContentPanel = settingsPanel;
            this.topHeaderPanel = topHeader;

            InitializeTabs();
        }

        private void InitializeTabs()
        {
            navTabsFlowPanel.Controls.Clear();

            btnDashboardTab = new ModernButton
            {
                Text = AppI18n.T("Puesto de Trabajo", "Workstation"),
                Size = new Size(160, 34),
                NormalColor = Color.FromArgb(14, 98, 115),
                HoverColor = Color.FromArgb(8, 70, 84),
                ForeColor = Color.White,
                BorderRadius = 6,
                Margin = new Padding(0, 2, 6, 0)
            };
            btnDashboardTab.Click += (s, e) => { SelectDashboardTab(); };

            btnNewTab = new ModernButton
            {
                Text = "  +  ",
                Size = new Size(38, 34),
                NormalColor = Color.FromArgb(241, 245, 249),
                HoverColor = Color.FromArgb(226, 232, 240),
                ForeColor = Color.FromArgb(15, 23, 42),
                BorderRadius = 6,
                Margin = new Padding(4, 2, 6, 0),
                Visible = false
            };
            btnNewTab.Click += (s, e) =>
            {
                SelectDashboardTab();
                if (OnNewTabClick != null) OnNewTabClick();
            };

            navTabsFlowPanel.Controls.Add(btnDashboardTab);
            navTabsFlowPanel.Controls.Add(btnNewTab);

            // Add dashboard & settings panels to container
            dashboardContentPanel.Dock = DockStyle.Fill;
            if (!contentContainerPanel.Controls.Contains(dashboardContentPanel))
            {
                contentContainerPanel.Controls.Add(dashboardContentPanel);
            }

            settingsContentPanel.Dock = DockStyle.Fill;
            if (!contentContainerPanel.Controls.Contains(settingsContentPanel))
            {
                contentContainerPanel.Controls.Add(settingsContentPanel);
            }
            settingsContentPanel.Visible = false;

            dashboardContentPanel.Visible = true;
            dashboardContentPanel.BringToFront();
        }

        public void SelectDashboardTab()
        {
            currentSelectedTab = null;
            isSettingsViewActive = false;

            topHeaderPanel.Visible = true;
            settingsContentPanel.Visible = false;

            HideAllSessionActionsPanels();

            foreach (Control c in contentContainerPanel.Controls)
            {
                if (c != dashboardContentPanel) c.Visible = false;
            }

            dashboardContentPanel.Visible = true;
            dashboardContentPanel.BringToFront();
            UpdateHeaderStyles();
        }

        public void SelectSettingsTab()
        {
            currentSelectedTab = null;
            isSettingsViewActive = true;

            topHeaderPanel.Visible = true;
            dashboardContentPanel.Visible = false;

            HideAllSessionActionsPanels();

            foreach (Control c in contentContainerPanel.Controls)
            {
                if (c != settingsContentPanel) c.Visible = false;
            }

            settingsContentPanel.Visible = true;
            settingsContentPanel.BringToFront();
            UpdateHeaderStyles();
        }

        private void HideAllSessionActionsPanels()
        {
            try
            {
                Control navHeader = navTabsFlowPanel.Parent;
                if (navHeader != null)
                {
                    foreach (var item in activeTabs)
                    {
                        if (item.SessionView != null && item.SessionView.SessionActionsPanel != null)
                        {
                            if (navHeader.Controls.Contains(item.SessionView.SessionActionsPanel))
                            {
                                navHeader.Controls.Remove(item.SessionView.SessionActionsPanel);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        public void AddSessionTab(RemoteSessionView sessionView)
        {
            string tabTitle = string.Format("{0} ({1})", sessionView.Hostname, sessionView.TargetId);

            Panel tabCard = new Panel
            {
                Size = new Size(200, 36),
                BackColor = Color.FromArgb(241, 245, 249),
                Margin = new Padding(0, 2, 6, 0)
            };

            ModernButton btnTab = new ModernButton
            {
                Text = tabTitle,
                Size = new Size(168, 34),
                Location = new Point(0, 0),
                NormalColor = Color.FromArgb(241, 245, 249),
                HoverColor = Color.FromArgb(226, 232, 240),
                ForeColor = Color.FromArgb(15, 23, 42),
                BorderRadius = 6,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };

            Button btnClose = new Button
            {
                Text = "âœ•",
                Size = new Size(22, 22),
                Location = new Point(172, 6),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };
            btnClose.FlatAppearance.BorderSize = 0;

            SessionTabItem tabItem = new SessionTabItem
            {
                Title = tabTitle,
                SessionView = sessionView,
                TabButton = btnTab,
                CloseButton = btnClose
            };

            btnTab.Click += (s, e) => { SelectSessionTab(tabItem); };
            btnClose.Click += (s, e) => { CloseSessionTab(tabItem); };

            sessionView.OnCloseSessionRequested = () =>
            {
                this.BeginInvoke((MethodInvoker)delegate { CloseSessionTab(tabItem); });
            };

            tabCard.Controls.Add(btnTab);
            tabCard.Controls.Add(btnClose);

            activeTabs.Add(tabItem);

            // Re-order tab flow: Dashboard -> Active Session Tabs -> [+] New Tab Button
            navTabsFlowPanel.Controls.Remove(btnNewTab);
            navTabsFlowPanel.Controls.Add(tabCard);
            navTabsFlowPanel.Controls.Add(btnNewTab);
            btnNewTab.Visible = true;

            // Add RemoteSessionView control to container
            sessionView.Dock = DockStyle.Fill;
            contentContainerPanel.Controls.Add(sessionView);

            SelectSessionTab(tabItem);
        }

        public void SelectSessionTab(SessionTabItem tabItem)
        {
            if (tabItem == null || !activeTabs.Contains(tabItem)) return;

            currentSelectedTab = tabItem;
            isSettingsViewActive = false;

            // Automatically maximize window to present full remote desktop experience
            try
            {
                Form parentForm = this.FindForm();
                if (parentForm != null)
                {
                    parentForm.WindowState = FormWindowState.Maximized;
                }
            }
            catch { }

            // Hide top header banner to give maximum vertical screen space to remote desktop!
            topHeaderPanel.Visible = false;
            dashboardContentPanel.Visible = false;
            settingsContentPanel.Visible = false;

            HideAllSessionActionsPanels();

            Control navHeader = navTabsFlowPanel.Parent;
            if (navHeader != null && tabItem.SessionView != null && tabItem.SessionView.SessionActionsPanel != null)
            {
                tabItem.SessionView.SessionActionsPanel.Dock = DockStyle.Right;
                navHeader.Controls.Add(tabItem.SessionView.SessionActionsPanel);
                tabItem.SessionView.SessionActionsPanel.BringToFront();
                tabItem.SessionView.SessionActionsPanel.Visible = true;
            }

            foreach (var item in activeTabs)
            {
                if (item == tabItem)
                {
                    item.SessionView.Visible = true;
                    item.SessionView.BringToFront();
                    item.SessionView.Focus();
                }
                else
                {
                    item.SessionView.Visible = false;
                }
            }

            UpdateHeaderStyles();
        }

        public void CloseSessionTab(SessionTabItem tabItem)
        {
            if (tabItem == null || !activeTabs.Contains(tabItem)) return;

            if (tabItem.SessionView != null)
            {
                Control navHeader = navTabsFlowPanel.Parent;
                if (navHeader != null && tabItem.SessionView.SessionActionsPanel != null)
                {
                    if (navHeader.Controls.Contains(tabItem.SessionView.SessionActionsPanel))
                    {
                        navHeader.Controls.Remove(tabItem.SessionView.SessionActionsPanel);
                    }
                }
                tabItem.SessionView.CloseSession();
            }

            activeTabs.Remove(tabItem);

            Control parentCard = tabItem.TabButton.Parent;
            if (parentCard != null)
            {
                navTabsFlowPanel.Controls.Remove(parentCard);
                parentCard.Dispose();
            }

            contentContainerPanel.Controls.Remove(tabItem.SessionView);
            tabItem.SessionView.Dispose();

            if (activeTabs.Count == 0)
            {
                btnNewTab.Visible = false;
                SelectDashboardTab();
            }
            else if (currentSelectedTab == tabItem)
            {
                SelectSessionTab(activeTabs[activeTabs.Count - 1]);
            }
            else
            {
                UpdateHeaderStyles();
            }
        }

        public void UpdateHeaderStyles()
        {
            bool isDashSelected = (currentSelectedTab == null && !isSettingsViewActive);
            btnDashboardTab.NormalColor = isDashSelected ? Color.FromArgb(14, 98, 115) : Color.FromArgb(241, 245, 249);
            btnDashboardTab.HoverColor = isDashSelected ? Color.FromArgb(8, 70, 84) : Color.FromArgb(226, 232, 240);
            btnDashboardTab.ForeColor = isDashSelected ? Color.White : Color.FromArgb(15, 23, 42);
            btnDashboardTab.Invalidate();

            foreach (var item in activeTabs)
            {
                bool isSelected = (item == currentSelectedTab);
                item.TabButton.NormalColor = isSelected ? Color.FromArgb(14, 98, 115) : Color.FromArgb(241, 245, 249);
                item.TabButton.HoverColor = isSelected ? Color.FromArgb(8, 70, 84) : Color.FromArgb(226, 232, 240);
                item.TabButton.ForeColor = isSelected ? Color.White : Color.FromArgb(15, 23, 42);
                item.TabButton.Invalidate();
            }
        }
    }
}