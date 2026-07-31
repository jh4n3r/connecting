using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using Conecting.Common;

namespace Conecting
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
        private Stream activeStream;

        public HostSessionFloatingWidget(string requestingId, Stream stream, Action onClose)
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
                Text = AppI18n.T("SESIÓN ACTIVA", "ACTIVE SESSION"),
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
