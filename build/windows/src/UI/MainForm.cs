using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
namespace Conecting
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
        private TextBox txtRelayHost;
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
                    trayIcon.ShowBalloonTip(2000, "Connecting", AppI18n.T("La aplicación sigue activa en segundo plano.", "Connecting is running in system tray."), ToolTipIcon.Info);
                }
            };
        }

        private void RestoreFromTray()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (trayIcon != null)
            {
                try
                {
                    trayIcon.Visible = false;
                    trayIcon.Dispose();
                    trayIcon = null;
                }
                catch { }
            }
            base.OnFormClosed(e);
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

                        int targetPort;
                        string targetHost = PeerResolver.GetRelayServerHost(out targetPort);
                        IAsyncResult ar = relayClient.BeginConnect(targetHost, targetPort, null, null);
                        if (ar.AsyncWaitHandle.WaitOne(2500) && relayClient.Connected)
                        {
                            Stream ns = PeerResolver.GetSecuredStream(relayClient, targetHost);
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
                                    string[] incomingParts = msg.Split(':');
                                    string requestingId = incomingParts.Length > 1 ? incomingParts[1].Trim() : "";
                                    string clientPsk = incomingParts.Length > 2 ? incomingParts[2].Trim() : "";
                                    string localPsk = PeerResolver.GetCustomPsk();
                                    bool accepted = false;
                                    bool isUnattended = false;
                                    this.Invoke((MethodInvoker)delegate
                                    {
                                        isUnattended = chkUnattendedAccess != null && chkUnattendedAccess.Checked;
                                    });

                                    if (isUnattended)
                                    {
                                        if (!string.IsNullOrEmpty(localPsk) && !string.Equals(clientPsk, localPsk, StringComparison.Ordinal))
                                        {
                                            byte[] rejBytes = Encoding.UTF8.GetBytes("REJECTED:INVALID_PSK\n");
                                            try { ns.Write(rejBytes, 0, rejBytes.Length); ns.Flush(); } catch { }
                                            break;
                                        }
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
                                        Stream activeStream = ns;

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
            this.Text = "Connecting - Solución de Escritorio Remoto";
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
                Text = AppI18n.T("Plataforma de Control Remoto Portátil y Segura", "Portable and Secure Remote Desktop Platform"),
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

            // AnyDesk-Style Hamburger Menu Button [ ≡ ]
            ModernButton btnHamburgerMenu = new ModernButton
            {
                Text = " ≡ ",
                Dock = DockStyle.Right,
                Width = 44,
                NormalColor = Color.FromArgb(241, 245, 249),
                HoverColor = Color.FromArgb(226, 232, 240),
                ForeColor = ColorTextDark,
                BorderRadius = 6,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold)
            };

            ContextMenuStrip menuHamburger = new ContextMenuStrip();
            menuHamburger.Items.Add(AppI18n.T("⚙ Configuración & Seguridad", "⚙ Settings & Security"), null, (s, e) =>
            {
                if (sessionTabControl != null) sessionTabControl.SelectSettingsTab();
            });

            bool isAdmin = IsUserAnAdmin();
            menuHamburger.Items.Add(isAdmin ? AppI18n.T("🛡 Modo Administrador (Activo)", "🛡 Administrator Mode (Active)") : AppI18n.T("🛡 Reiniciar como Administrador", "🛡 Restart as Administrator"), null, (s, e) =>
            {
                if (!isAdmin)
                {
                    if (MessageBox.Show(AppI18n.T("¿Desea reiniciar la aplicación con permisos de Administrador?", "Restart application with Administrator permissions?"), "Connecting", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
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
            menuHamburger.Items.Add(AppI18n.T("❓ Acerca de Connecting...", "❓ About Connecting..."), null, (s, e) =>
            {
                using (AboutForm about = new AboutForm()) { about.ShowDialog(); }
            });
            menuHamburger.Items.Add(AppI18n.T("📖 Ayuda y Documentación", "📖 Help & Documentation"), null, (s, e) =>
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
                if (MessageBox.Show(AppI18n.T("¿Está seguro de que desea generar una nueva ID permanente para este puesto?", "Are you sure you want to generate a new permanent ID for this computer?"), "Connecting", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
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
                    if (MessageBox.Show(AppI18n.T("¿Desea reiniciar la aplicación con permisos elevados de Administrador?", "Restart application with elevated Administrator permissions?"), "Connecting", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
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
                if (MessageBox.Show(AppI18n.T("¿Desea borrar todo el historial de conexiones recientes?", "Clear all recent connection history?"), "Connecting", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
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
                    Text = AppI18n.T("No hay conexiones recientes aún.", "No recent connections yet."),
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

                Button btnEditAlias = new Button { Text = "✏", Location = new Point(148, 6), Size = new Size(26, 24), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
                btnEditAlias.FlatAppearance.BorderSize = 0;

                Button btnDelete = new Button { Text = "✕", Location = new Point(176, 6), Size = new Size(26, 24), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, ForeColor = Color.FromArgb(225, 29, 72) };
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
                    if (MessageBox.Show(string.Format(AppI18n.T("¿Eliminar puesto ID ({0}) del historial?", "Delete ID ({0}) from history?"), currentTargetId), "Connecting", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
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
            Label lblSecHeader = new Label { Text = AppI18n.T("Configuración Global de Seguridad y Acceso Desatendido", "Global Security & Unattended Access Settings"), Font = new Font("Segoe UI", 13F, FontStyle.Bold), Location = new Point(24, 20), AutoSize = true, ForeColor = ColorTextDark };

            chkUnattendedAccess = new CheckBox { Text = AppI18n.T("Permitir Acceso Desatendido directo con Clave PSK (sin confirmación)", "Allow direct unattended access with PSK Key (no prompt)"), Checked = true, Location = new Point(24, 65), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            
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

            Label lblUserAliasLabel = new Label { Text = AppI18n.T("Nombre de Presentación (Alias en Chat y Conexión):", "Display Name (Chat & Connection Alias):"), Location = new Point(24, 150), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
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
            
            Label lblLang = new Label { Text = AppI18n.T("Idioma de la Aplicación:", "Application Language:"), Location = new Point(24, 55), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            ComboBox cboLang = new ComboBox { Location = new Point(190, 52), Size = new Size(150, 28), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 10F) };
            cboLang.Items.Add("Español (ES)");
            cboLang.Items.Add("English (EN)");
            
            string savedLang = PeerResolver.GetSavedLanguage();
            cboLang.SelectedIndex = (savedLang == "en") ? 1 : 0;
            cboLang.SelectedIndexChanged += (s, e) =>
            {
                string sel = (cboLang.SelectedIndex == 1) ? "en" : "es";
                if (sel != PeerResolver.GetSavedLanguage())
                {
                    PeerResolver.SaveLanguage(sel);
                    MessageBox.Show(AppI18n.T("La aplicación se reiniciará para aplicar los cambios de idioma.", "The application will restart to apply language changes."), "Connecting", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Application.Restart();
                }
            };

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
                    if (MessageBox.Show(AppI18n.T("¿Desea detener y desinstalar el Servicio de Windows ConnectingService?", "Stop and uninstall ConnectingService Windows Service?"), "Connecting", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
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
                    if (MessageBox.Show(AppI18n.T("¿Desea crear e iniciar el Servicio de Windows ConnectingService?", "Create and start ConnectingService Windows Service?"), "Connecting", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
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

            Label lblRelayHostLabel = new Label { Text = AppI18n.T("Servidor Relay Personalizado (Dominio o IP):", "Custom Relay Server (Domain or IP):"), Location = new Point(24, 108), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            txtRelayHost = new TextBox
            {
                Location = new Point(370, 105),
                Size = new Size(230, 28),
                Font = new Font("Segoe UI", 10F),
                Text = PeerResolver.GetCustomRelayHost()
            };
            txtRelayHost.Leave += (s, e) =>
            {
                string host = txtRelayHost.Text.Trim();
                if (!string.IsNullOrEmpty(host))
                {
                    PeerResolver.SaveCustomRelayHost(host);
                    try { if (currentHostRelayClient != null) currentHostRelayClient.Close(); } catch { }
                }
            };
            ModernButton btnSaveRelay = new ModernButton
            {
                Text = AppI18n.T("Guardar Servidor", "Save Server"),
                Location = new Point(610, 102),
                Size = new Size(140, 32),
                NormalColor = ColorCyanPrimary,
                HoverColor = ColorCyanDark,
                BorderRadius = 6
            };
            btnSaveRelay.Click += (s, e) =>
            {
                string host = txtRelayHost.Text.Trim();
                PeerResolver.SaveCustomRelayHost(host);
                try
                {
                    if (currentHostRelayClient != null)
                    {
                        currentHostRelayClient.Close();
                    }
                }
                catch { }
                MessageBox.Show(
                    AppI18n.T("Servidor Relay personalizado guardado correctamente. Reconectando al nuevo servidor...", "Custom Relay Server saved successfully. Reconnecting to new server..."),
                    "Connecting",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                StartRelayHostRegistration();
            };

            cardService.Controls.Add(lblSvcHeader);
            cardService.Controls.Add(lblLang);
            cardService.Controls.Add(cboLang);
            cardService.Controls.Add(btnInstallSvc);
            cardService.Controls.Add(lblRelayHostLabel);
            cardService.Controls.Add(txtRelayHost);
            cardService.Controls.Add(btnSaveRelay);

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
                            string clientPsk = parts.Length >= 4 ? parts[3].Trim() : "";
                            string localPsk = PeerResolver.GetCustomPsk();

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
                                    if (!string.IsNullOrEmpty(localPsk) && !string.Equals(clientPsk, localPsk, StringComparison.Ordinal))
                                    {
                                        byte[] rejBuf = Encoding.UTF8.GetBytes("REJECTED:INVALID_PSK\n");
                                        try { stream.Write(rejBuf, 0, rejBuf.Length); stream.Flush(); } catch { }
                                        incomingClient.Close();
                                        continue;
                                    }
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
            if (txtRelayHost != null && !string.IsNullOrEmpty(txtRelayHost.Text.Trim()))
            {
                PeerResolver.SaveCustomRelayHost(txtRelayHost.Text.Trim());
            }

            string rawInput = txtRemoteId.Text.Trim();
            if (string.IsNullOrEmpty(rawInput))
            {
                MessageBox.Show(AppI18n.T("Por favor introduzca la ID remota de 9 dígitos.", "Please enter the 9-digit remote ID."), "Connecting", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string pskInput = txtRemotePsk.Text.Trim();
            if (string.IsNullOrEmpty(pskInput))
            {
                MessageBox.Show(AppI18n.T("La Clave PSK es OBLIGATORIA para iniciar una sesión remota.", "PSK Key is REQUIRED to start a remote session."), "Connecting", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                    Stream securedStream;
                    TcpClient client = PeerResolver.DiscoverAndConnectPeer(rawInput, rawNumId, pskInput, out remoteHostname, out errorMsg, out securedStream);

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
                        RemoteSessionView sessionView = new RemoteSessionView(rawInput, remoteHostname, pskInput, rawNumId, client, securedStream);
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
