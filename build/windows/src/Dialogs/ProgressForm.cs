using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace Conecting
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
                Text = AppI18n.T("Estableciendo conexión en tiempo real...\nEsperando respuesta del equipo remoto.", "Establishing real-time connection...\nWaiting for remote computer response."),
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
