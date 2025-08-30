using System.Drawing.Drawing2D;

namespace vmMedia
{
    public class OverlayForm : Form
    {
        private readonly System.Windows.Forms.Timer _hideTimer;
        private readonly System.Windows.Forms.Timer _fadeTimer;
        private string _title = string.Empty;
        private string _subtitle = string.Empty;
        private bool _showBar;
        private float _level;
        private bool _muted;
        private float _targetOpacity;
        private OverlayPalette _palette;
        public OverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Opacity = 0;
            _targetOpacity = 0.88f;
            BackColor = Color.Black;
            TransparencyKey = Color.Magenta;
            _hideTimer = new System.Windows.Forms.Timer { Interval = 1400 };
            _hideTimer.Tick += (_, _) => BeginFadeOut();
            _fadeTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _fadeTimer.Tick += (_, _) => StepFade();
            Width = 360;
            Height = 82;
            Position();
            UpdateWindowRegion();
            _palette = ThemeManager.GetPalette();
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateWindowRegion();
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_NOACTIVATE = 0x08000000;
                const int WS_EX_TOOLWINDOW = 0x00000080;
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        private void Position()
        {
            var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            var x = wa.X + (wa.Width - Width) / 2;
            var margin = 40;
            var y = wa.Bottom - Height - margin;
            if (y < wa.Y) y = wa.Y;
            Location = new Point(x, y);
        }

        public void ShowVolume(string stripName, float gainDb, bool muted)
        {
            _title = stripName;
            _subtitle = muted ? "Muted" : FormatDb(gainDb);
            _muted = muted;
            _showBar = true;
            _level = NormalizeDb(gainDb);
            Position();
            FadeInAndShow();
        }

        public void ShowMessage(string message)
        {
            _title = message;
            _subtitle = string.Empty;
            _showBar = false;
            _muted = false;
            Position();
            FadeInAndShow();
        }

        private void FadeInAndShow()
        {
            _hideTimer.Stop();
            _fadeTimer.Stop();
            if (!Visible) Show();
            _targetOpacity = 0.95f;
            Opacity = Math.Min(_targetOpacity, Math.Max(Opacity, 0.1));
            _fadeTimer.Start();
            _hideTimer.Start();
            Invalidate();
        }

        private void BeginFadeOut()
        {
            _hideTimer.Stop();
            _targetOpacity = 0;
            _fadeTimer.Start();
        }

        private void StepFade()
        {
            var step = 0.08;
            if (Opacity < _targetOpacity)
            {
                Opacity = Math.Min(_targetOpacity, Opacity + step);
            }
            else if (Opacity > _targetOpacity)
            {
                Opacity = Math.Max(_targetOpacity, Opacity - step);
            }
            else
            {
                _fadeTimer.Stop();
                if (_targetOpacity <= 0)
                {
                    Hide();
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = ClientRectangle;
            var bg = new Rectangle(0, 0, rect.Width - 1, rect.Height - 1);
            using var path = Rounded(bg, 12);
            using var bgBrush = new SolidBrush(_palette.Background);
            using var borderPen = new Pen(_palette.Border);
            e.Graphics.FillPath(bgBrush, path);
            e.Graphics.DrawPath(borderPen, path);

            var margin = 10;
            var iconSize = 28;
            var y = margin;
            var x = margin;
            var used = DrawSpeakerIcon(e.Graphics, new Rectangle(x, y, iconSize, iconSize), _muted, _level);
            var iconColumn = (int)Math.Ceiling(iconSize * 1.3f);
            x = margin + iconColumn + 8;

            var family = SystemFonts.MessageBoxFont?.FontFamily ?? FontFamily.GenericSansSerif;
            using var titleFont = new Font(family, 10.5f, FontStyle.Bold);
            using var subFont = new Font(family, 9f, FontStyle.Regular);
            using var titleBrush = new SolidBrush(_palette.Title);
            using var subBrush = new SolidBrush(_palette.Subtitle);
            e.Graphics.DrawString(_title, titleFont, titleBrush, new PointF(x, y));
            if (!string.IsNullOrEmpty(_subtitle))
            {
                e.Graphics.DrawString(_subtitle, subFont, subBrush, new PointF(x, y + 14));
            }

            if (_showBar)
            {
                var titleH = titleFont.GetHeight(e.Graphics);
                var subH = string.IsNullOrEmpty(_subtitle) ? 0f : subFont.GetHeight(e.Graphics);
                var gap = 6;
                var barX = x;
                var barW = rect.Width - x - margin;
                var barH = 6;
                var barY = y + (int)Math.Ceiling(titleH + (subH > 0 ? subH + gap : gap));
                var radius = 3;
                var backRect = new Rectangle(barX, barY, barW, barH);
                int fillWidth = (int)(barW * Math.Clamp(_level, 0f, 1f));
                using var backPath = Rounded(backRect, radius);
                using var backBrush = new SolidBrush(_palette.BarBack);
                e.Graphics.FillPath(backBrush, backPath);
                if (fillWidth > 0)
                {
                    var fillRect = new Rectangle(barX, barY, fillWidth, barH);
                    using var fillPath = Rounded(fillRect, radius);
                    using var fillBrush = new LinearGradientBrush(fillRect, _palette.BarFillStart, _palette.BarFillEnd, LinearGradientMode.Horizontal);
                    e.Graphics.FillPath(fillBrush, fillPath);
                }
            }
        }

        private static GraphicsPath Rounded(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            var d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static int DrawSpeakerIcon(Graphics g, Rectangle bounds, bool muted, float level)
        {
            using var path = new GraphicsPath();
            var w = bounds.Width;
            var h = bounds.Height;
            var x = bounds.X;
            var y = bounds.Y;
            float maxRight = x;
            var horn = new PointF[]
            {
                new PointF(x + w * 0.10f, y + h * 0.36f),
                new PointF(x + w * 0.34f, y + h * 0.36f),
                new PointF(x + w * 0.58f, y + h * 0.20f),
                new PointF(x + w * 0.58f, y + h * 0.80f),
                new PointF(x + w * 0.34f, y + h * 0.64f),
                new PointF(x + w * 0.10f, y + h * 0.64f)
            };
            path.AddPolygon(horn);
            using var brush = new SolidBrush(ThemeManager.GetPalette().Icon);
            g.FillPath(brush, path);
            maxRight = Math.Max(maxRight, x + w * 0.58f);
            if (muted)
            {
                using var pen = new Pen(ThemeManager.GetPalette().MuteX, 2f);
                g.DrawLine(pen, x + w * 0.70f, y + h * 0.25f, x + w * 0.95f, y + h * 0.75f);
                g.DrawLine(pen, x + w * 0.95f, y + h * 0.25f, x + w * 0.70f, y + h * 0.75f);
            }
            else
            {
                int waves = level < 0.08f ? 0 : level < 0.35f ? 1 : level < 0.7f ? 2 : 3;
                var cx = x + w * 0.60f;
                var cy = y + h * 0.5f;
                for (int i = 1; i <= waves; i++)
                {
                    float rx = w * (0.095f * i);
                    float ry = h * (0.5f * i);
                    var rect = new RectangleF(cx, cy - ry, rx * 2, ry * 2);
                    int a = 160 + i * 22;
                    if (a > 255) a = 255;
                    var iconColor = ThemeManager.GetPalette().Icon;
                    using var wavePen = new Pen(Color.FromArgb(a, iconColor), 1.5f);
                    g.DrawArc(wavePen, rect, -38, 76);
                    maxRight = Math.Max(maxRight, rect.Right);
                }
            }
            return (int)Math.Ceiling(maxRight - x);
        }

        private static float NormalizeDb(float db)
        {
            var clamped = Math.Max(-60f, Math.Min(12f, db));
            return (clamped + 60f) / 72f;
        }

        private static string FormatDb(float db)
        {
            var rounded = Math.Round(db);
            return rounded > 0 ? $"+{rounded:0} dB" : $"{rounded:0} dB";
        }

        private void UpdateWindowRegion()
        {
            var rect = ClientRectangle;
            if (rect.Width <= 0 || rect.Height <= 0) return;
            using var path = Rounded(new Rectangle(0, 0, rect.Width, rect.Height), 12);
            Region = new Region(path);
        }

        private void OnThemeChanged()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(OnThemeChanged));
                return;
            }
            _palette = ThemeManager.GetPalette();
            Invalidate();
        }
    }
}
