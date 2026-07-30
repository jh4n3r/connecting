using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Conecting.Common;
using Conecting.UI;

namespace Conecting.Dialogs
{
    public class ConnectionRequestForm : Form
    {
        private ModernButton btnAccept;
        private ModernButton btnReject;
        public bool IsAccepted { get; private set; }

        public ConnectionRequestForm(string requestingPeerId)
        {
            this.Text = AppI18n.T("Solicitud de Conexión Entrante", "Incoming Connection Request");
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
                Text = AppI18n.T("¡Solicitud de Control Remoto!", "Remote Control Request!"),
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
                Text = AppI18n.T("Controlar teclado y ratón en tiempo real", "Control mouse and keyboard in real time"),
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
