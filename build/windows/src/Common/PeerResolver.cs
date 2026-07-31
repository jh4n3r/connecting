using System;
using System.Diagnostics;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Win32;

namespace Conecting
{
    public static class PeerResolver
    {
        private static readonly string AppDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "ConnectingNodes"
        );

        // DEFAULT RELAY SERVER DOMAIN (Generic Open Source Default)
        public static string RelayServerDomain = "your-relay-server.com";
        public static int RelayServerPort = 8443;

        private static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        public static Stream GetSecuredStream(TcpClient client, string targetHost)
        {
            NetworkStream rawStream = client.GetStream();
            SslStream sslStream = new SslStream(
                rawStream, 
                false, 
                new RemoteCertificateValidationCallback(ValidateServerCertificate)
            );
            sslStream.AuthenticateAsClient(targetHost, null, SslProtocols.Tls12, false);
            return sslStream;
        }

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
                File.WriteAllText(path, host == null ? "" : host.Trim());
            }
            catch { }
        }

        public static string GetRelayServerHost(out int port)
        {
            port = RelayServerPort;
            try
            {
                EnsureDirectoryExists();
                string[] files = new string[] { "relayhost.dat", "server_host.dat" };
                foreach (string f in files)
                {
                    string path = Path.Combine(AppDataDirectory, f);
                    if (File.Exists(path))
                    {
                        string saved = File.ReadAllText(path).Trim();
                        if (!string.IsNullOrEmpty(saved))
                        {
                            if (saved.Contains(":"))
                            {
                                string[] parts = saved.Split(':');
                                int parsedPort;
                                if (parts.Length > 1 && int.TryParse(parts[1], out parsedPort))
                                {
                                    port = parsedPort;
                                }
                                return parts[0].Trim();
                            }
                            return saved;
                        }
                    }
                }
            }
            catch { }
            return RelayServerDomain;
        }

        public static string GetRelayServerHost()
        {
            int dummyPort;
            return GetRelayServerHost(out dummyPort);
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
                    long dummy;
                    if (saved.Length == 9 && long.TryParse(saved, out dummy)) return saved;
                }
                
                string newId = Generate9DigitId();
                File.WriteAllText(path, newId);
                return newId;
            }
            catch
            {
                return Generate9DigitId();
            }
        }

        public static void SavePersistentId(string id)
        {
            try
            {
                EnsureDirectoryExists();
                string path = Path.Combine(AppDataDirectory, "node_id.dat");
                File.WriteAllText(path, id.Trim());
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

        public static int GetSessionLimit()
        {
            try
            {
                EnsureDirectoryExists();
                string path = Path.Combine(AppDataDirectory, "session_limit.dat");
                if (File.Exists(path))
                {
                    int limit;
                    if (int.TryParse(File.ReadAllText(path).Trim(), out limit))
                    {
                        return Math.Max(1, limit);
                    }
                }
            }
            catch { }
            return 1;
        }

        public static void SaveSessionLimit(int limit)
        {
            try
            {
                EnsureDirectoryExists();
                string path = Path.Combine(AppDataDirectory, "session_limit.dat");
                File.WriteAllText(path, Math.Max(1, limit).ToString());
            }
            catch { }
        }

        public static string GetSavedLanguage()
        {
            try
            {
                EnsureDirectoryExists();
                string path = Path.Combine(AppDataDirectory, "language.dat");
                if (File.Exists(path))
                {
                    string saved = File.ReadAllText(path).Trim();
                    if (!string.IsNullOrEmpty(saved)) return saved;
                }
            }
            catch { }
            return "es";
        }

        public static void SaveLanguage(string lang)
        {
            try
            {
                EnsureDirectoryExists();
                string path = Path.Combine(AppDataDirectory, "language.dat");
                File.WriteAllText(path, lang == null ? "es" : lang.Trim().ToLower());
            }
            catch { }
        }

        public static string GetUserDisplayName()
        {
            try
            {
                EnsureDirectoryExists();
                string path = Path.Combine(AppDataDirectory, "user_display_name.dat");
                if (File.Exists(path))
                {
                    string saved = File.ReadAllText(path).Trim();
                    if (!string.IsNullOrEmpty(saved)) return saved;
                }
            }
            catch { }
            return Environment.MachineName;
        }

        public static void SaveUserDisplayName(string name)
        {
            try
            {
                EnsureDirectoryExists();
                string path = Path.Combine(AppDataDirectory, "user_display_name.dat");
                File.WriteAllText(path, name == null ? Environment.MachineName : name.Trim());
            }
            catch { }
        }

        public static string GenerateRandom9DigitId()
        {
            return Generate9DigitId();
        }

        public static bool IsWindowsServiceInstalled(string serviceName = "ConnectingService")
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + (string.IsNullOrEmpty(serviceName) ? "ConnectingService" : serviceName)))
                {
                    return key != null;
                }
            }
            catch { }
            return false;
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

        public static TcpClient DiscoverAndConnectPeer(string targetId, string myId, string pskKey, out string remoteHostname, out string errorMsg, out Stream securedStream)
        {
            remoteHostname = "PC-REMOTO";
            errorMsg = "";
            securedStream = null;
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

                int targetPort;
                string targetHost = GetRelayServerHost(out targetPort);
                IAsyncResult ar = client.BeginConnect(targetHost, targetPort, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(5000) || !client.Connected)
                {
                    try { client.Close(); } catch { }
                    errorMsg = string.Format(AppI18n.T(
                        "No se pudo establecer conexión con el Servidor Relay ({0}:{1}).",
                        "Could not connect to Relay Server ({0}:{1})."
                    ), targetHost, targetPort);
                    return null;
                }

                Stream ns = GetSecuredStream(client, targetHost);
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
                    string[] parts = resp.Split(':');
                    if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
                    {
                        remoteHostname = parts[1].Trim();
                    }
                    securedStream = ns;
                    return client;
                }
                else if (resp.StartsWith("REJECTED:INVALID_PSK"))
                {
                    client.Close();
                    errorMsg = AppI18n.T("Clave PSK incorrecta. Acceso denegado por el host remoto.", "Incorrect PSK Key. Access denied by remote host.");
                    return null;
                }
                else if (resp.StartsWith("REJECTED"))
                {
                    client.Close();
                    errorMsg = AppI18n.T("El usuario remoto rechazó la solicitud de conexión.", "The remote user rejected the connection request.");
                    return null;
                }
                else if (resp.StartsWith("ERROR:"))
                {
                    client.Close();
                    errorMsg = resp.Substring(6);
                    return null;
                }
                else
                {
                    client.Close();
                    errorMsg = string.Format(AppI18n.T("Respuesta no válida del servidor: {0}", "Invalid response from server: {0}"), resp);
                    return null;
                }
            }
            catch (Exception ex)
            {
                errorMsg = string.Format(AppI18n.T("Error de red: {0}", "Network error: {0}"), ex.Message);
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

        private static string Generate9DigitId()
        {
            Random rng = new Random();
            return rng.Next(100, 999).ToString() + rng.Next(100, 999).ToString() + rng.Next(100, 999).ToString();
        }
    }
}
