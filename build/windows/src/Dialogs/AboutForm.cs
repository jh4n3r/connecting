using System;
using System.Drawing;
using System.Windows.Forms;
using Conecting.Common;
using Conecting.UI;

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
            Label lblVer = new Label { Text = AppI18n.T("Versión 1.0.2 (Build 2026)", "Version 1.0.2 (Build 2026)"), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), Location = new Point(20, 52), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) };
            Label lblDesc = new Label
            {
                Text = AppI18n.T(
                    "Plataforma de Escritorio Remoto Abierta, Portable y Segura.\nDiseñado para ofrecer asistencia técnica nativa sin dependencias.",
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
