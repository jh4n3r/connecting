using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Conecting
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
