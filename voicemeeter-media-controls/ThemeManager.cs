using Microsoft.Win32;

namespace vmMedia
{
    public static class ThemeManager
    {
        private static bool _isLight;
        public static event Action? ThemeChanged;

        static ThemeManager()
        {
            _isLight = GetIsLightTheme();
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }

        public static bool IsLight => _isLight;

        private static void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
        {
            bool light = GetIsLightTheme();
            if (light != _isLight)
            {
                _isLight = light;
                ThemeChanged?.Invoke();
            }
        }

        private static bool GetIsLightTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize");
                if (key != null)
                {
                    var v = key.GetValue("AppsUseLightTheme");
                    if (v is int i)
                    {
                        return i != 0;
                    }
                }
            }
            catch { }
            return true;
        }

        public static OverlayPalette GetPalette()
        {
            if (_isLight)
            {
                return new OverlayPalette
                {
                    Background = Color.FromArgb(220, 245, 245, 245),
                    Border = Color.FromArgb(60, 0, 0, 0),
                    Title = Color.FromArgb(255, 20, 20, 20),
                    Subtitle = Color.FromArgb(255, 60, 60, 60),
                    Icon = Color.FromArgb(255, 20, 20, 20),
                    MuteX = Color.Red,
                    BarBack = Color.FromArgb(120, 0, 0, 0),
                    BarFillStart = Color.FromArgb(255, 0, 120, 240),
                    BarFillEnd = Color.FromArgb(255, 0, 90, 210)
                };
            }
            else
            {
                return new OverlayPalette
                {
                    Background = Color.FromArgb(220, 18, 18, 18),
                    Border = Color.FromArgb(45, 255, 255, 255),
                    Title = Color.FromArgb(255, 255, 255, 255),
                    Subtitle = Color.FromArgb(230, 210, 210, 210),
                    Icon = Color.FromArgb(255, 255, 255, 255),
                    MuteX = Color.Red,
                    BarBack = Color.FromArgb(70, 255, 255, 255),
                    BarFillStart = Color.FromArgb(255, 100, 150, 255),
                    BarFillEnd = Color.FromArgb(255, 0, 120, 240)
                };
            }
        }
    }

    public struct OverlayPalette
    {
        public Color Background { get; set; }
        public Color Border { get; set; }
        public Color Title { get; set; }
        public Color Subtitle { get; set; }
        public Color Icon { get; set; }
        public Color MuteX { get; set; }
        public Color BarBack { get; set; }
        public Color BarFillStart { get; set; }
        public Color BarFillEnd { get; set; }
    }
}
