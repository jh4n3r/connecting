using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace Conecting
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

        // DEFAULT RELAY SERVER DOMAIN (Generic Open Source Default)
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
                errorMsg = AppI18n.T("La ID introducida debe contener exactamente 9 dígitos.", "Entered ID must contain exactly 9 digits.");
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
                        "No se pudo establecer conexión con el Servidor Relay ({0}:{1}).",
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
                    errorMsg = AppI18n.T("El equipo remoto no respondió a la solicitud.", "Remote computer did not respond to request.");
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
                    errorMsg = AppI18n.T("El equipo remoto se encuentra en otra sesión activa.", "Remote computer is busy in another active session.");
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
                        "El equipo remoto ID ({0}) está fuera de línea o rechazó la conexión.",
                        "Remote computer ID ({0}) is offline or rejected connection."
                    ), cleanTargetId);
                    return null;
                }
            }
            catch (Exception ex)
            {
                errorMsg = AppI18n.T("Error de conexión: ", "Connection error: ") + ex.Message;
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
