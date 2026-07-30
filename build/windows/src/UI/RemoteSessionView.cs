using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
namespace Conecting
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
                Text = AppI18n.T("Menú", "Menu"),
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
                Text = AppI18n.T("Reconectando sesión en tiempo real...\nRestableciendo enlace con el host remoto.", "Reconnecting session in real time...\nRestoring link with remote host."),
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
            ToolStripMenuItem itemOriginal = new ToolStripMenuItem(AppI18n.T("Tamaño Original (1:1)", "Original Size (1:1)"));
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

            ToolStripMenuItem menuQualityHeader = new ToolStripMenuItem(AppI18n.T("Calidad de Transmisión:", "Transmission Quality:")) { Enabled = false };
            ToolStripMenuItem itemQualityBalanced = new ToolStripMenuItem(AppI18n.T("Balanceado (Recomendado)", "Balanced (Recommended)"));
            ToolStripMenuItem itemQualityBest = new ToolStripMenuItem(AppI18n.T("Mejor Aspecto (Alta Definición)", "Best Quality (High Definition)"));
            ToolStripMenuItem itemQualityFast = new ToolStripMenuItem(AppI18n.T("Rápida (Baja Latencia)", "Fast (Low Latency)"));

            Action updateQualityChecks = () =>
            {
                itemQualityBalanced.Text = (currentQualityLevel == 75 ? "✓ " : "  ") + AppI18n.T("Balanceado (Recomendado)", "Balanced (Recommended)");
                itemQualityBest.Text = (currentQualityLevel == 90 ? "✓ " : "  ") + AppI18n.T("Mejor Aspecto (Alta Definición)", "Best Quality (High Definition)");
                itemQualityFast.Text = (currentQualityLevel == 60 ? "✓ " : "  ") + AppI18n.T("Rápida (Baja Latencia)", "Fast (Low Latency)");
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
            menuMainMenu.Items.Add(AppI18n.T("Ayuda y Documentación", "Help & Documentation"), null, (s, e) =>
            {
                try { System.Diagnostics.Process.Start("https://jh4n3r.github.io/connecting/docs/"); } catch { }
            });
            menuMainMenu.Items.Add("-");
            menuMainMenu.Items.Add(AppI18n.T("Finalizar Sesión Remota", "End Remote Session"), null, (s, e) => { CloseSession(); });
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

            Label lblChatHeader = new Label { Text = "💬 Chat de Sesión Remota", Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), Location = new Point(12, 12), AutoSize = true, ForeColor = Color.FromArgb(14, 98, 115) };
            btnCloseChatDrawer = new ModernButton { Text = "✕", Location = new Point(260, 8), Size = new Size(28, 28), NormalColor = Color.FromArgb(239, 68, 68), HoverColor = Color.FromArgb(220, 38, 38), BorderRadius = 4 };
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
            Label lblTtyHeader = new Label { Text = "💻 Consola Interactiva TTY", Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), Location = new Point(12, 12), AutoSize = true, ForeColor = Color.FromArgb(0, 172, 193) };
            btnCloseTtyDrawer = new ModernButton { Text = "✕", Location = new Point(400, 8), Size = new Size(28, 28), NormalColor = Color.FromArgb(239, 68, 68), HoverColor = Color.FromArgb(220, 38, 38), BorderRadius = 4 };
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
                        string.Format(AppI18n.T("El equipo remoto ({0}) ha finalizado la sesión.", "Remote computer ({0}) ended the session."), TargetId),
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
