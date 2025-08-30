using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace vmMedia
{
    public partial class Tray : Form
    {
        private readonly NotifyIcon _trayIcon;
        private readonly ContextMenuStrip _trayMenu;
        private int _currentStrip = 5;
        private static readonly int[] _strips = [5, 6, 7]; // when using voicemeeter potato these are the virutal inputs
        private float amountToAdjust = 1.0f;
        private readonly OverlayForm _overlay = new();
        private static readonly List<(int vmType, int index, string name)> _stripNames = new() // voicemeeter type, index, default name // 0 = standard, 1 = banana, 2 = potato
        {
            (0, 0, "Stereo Input 1"),
            (0, 1, "Stereo Input 2"),
            (0, 2, "VB-Audio Voicemeeter VAIO"),
            (1, 0, "Stereo Input 1"),
            (1, 1, "Stereo Input 2"),
            (1, 2, "Stereo Input 3"),
            (1, 3, "Stereo Input 4"),
            (1, 4, "Stereo Input 5"),
            (1, 5, "Voicemeeter AUX"),
            (1, 6, "Voicemeeter VAIO3"),
            (2, 0, "Stereo Input 1"),
            (2, 1, "Stereo Input 2"),
            (2, 2, "Stereo Input 3"),
            (2, 3, "Stereo Input 4"),
            (2, 4, "Stereo Input 5"),
            (2, 5, "Voicemeeter Input"),
            (2, 6, "Voicemeeter AUX"),
            (2, 7, "Voicemeeter VAIO3")
        };
        //private static List<(int, string)> _currentStripNames => getStripNames();

        public Tray()
        {
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            Opacity = 0;

            _trayMenu = new ContextMenuStrip();
            var muteItem = _trayMenu.Items.Add("Toggle Mute", null, (_, _) =>
            {
                bool currentMute = GetMuteState(_currentStrip);
                MuteStrip(_currentStrip, !currentMute);
                float gain = GetGain(_currentStrip);
                string name = GetStripName(_currentStrip);
                ShowOverlayVolume(name, gain, !currentMute);
            });
            _trayMenu.Items.Add("Exit", null, (_, _) => Application.Exit());
            _trayMenu.Opening += (sender, e) =>
            {
                bool isMuted = GetMuteState(_currentStrip);
                string stripName = GetStripName(_currentStrip);
                string action = isMuted ? "Unmute" : "Mute";
                muteItem.Text = $"{action} {stripName}";
            };

            _trayIcon = new NotifyIcon
            {
                Text = "Voicemeeter Media",
                Icon = SystemIcons.Application,
                ContextMenuStrip = _trayMenu,
                Visible = true
            };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            InitVoicemeeter();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            _trayIcon.Visible = false;
            try { _overlay.Hide(); _overlay.Dispose(); } catch { }
            try { VoicemeeterRemote.VBVMR_Logout(); } catch { }
            if (_hookId != IntPtr.Zero)
            {
                try { UnhookWindowsHookEx(_hookId); } catch { }
                _hookId = IntPtr.Zero;
            }
        }

        private void InitVoicemeeter()
        {
            try
            {
                var login = VoicemeeterRemote.VBVMR_Login();
                if (login != 0)
                {
                    MessageBox.Show("Failed to login to Voicemeeter: " + login, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Exit();
                }
            }
            catch (DllNotFoundException)
            {
                if (File.Exists(@"C:\Program Files (x86)\VB\Voicemeeter\VoicemeeterRemote64.dll"))
                {
                    var lib = LoadLibrary(@"C:\Program Files (x86)\VB\Voicemeeter\VoicemeeterRemote64.dll");
                    if (lib == IntPtr.Zero)
                    {
                        MessageBox.Show("Failed to load VoicemeeterRemote64.dll", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Application.Exit();
                    }
                    else
                    {
                        var login = VoicemeeterRemote.VBVMR_Login();
                        if (login != 0)
                        {
                            MessageBox.Show("Failed to login to Voicemeeter: " + login, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            Application.Exit();
                        }
                    }
                }
            }
        }
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
            {
                KBDLLHOOKSTRUCT kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                switch (kb.vkCode)
                {
                    case VK_VOLUME_UP:
                        AdjustStrip(+amountToAdjust);
                        return 1;
                    case VK_VOLUME_DOWN:
                        AdjustStrip(-amountToAdjust);
                        return 1;
                    case VK_VOLUME_MUTE:
                        CycleStrip();
                        return 1;
                }
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private void AdjustStrip(float delta)
        {
            try
            {
                int strip = _currentStrip;
                float prevGain = GetGain(strip);
                string cmd = delta >= 0
                    ? $"Strip[{strip}].Gain += {delta}"
                    : $"Strip[{strip}].Gain -= {-delta}";
                int result = VoicemeeterRemote.VBVMR_SetParametersW(cmd);
                if (result != 0)
                {
                    Debug.WriteLine($"Failed to adjust gain: {result}");
                }
                float gain = MathF.Max(-60f, MathF.Min(12f, prevGain + delta));
                bool muted = GetMuteState(strip);
                string name = GetStripName(strip);
                ShowOverlayVolume(name, gain, muted);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception adjusting gain: {ex}");
            }
        }
        private void CycleStrip()
        {
            _currentStrip = _strips[(_strips.ToList().IndexOf(_currentStrip) + 1) % _strips.Length];
            string stripName = GetStripName(_currentStrip);
            float gain = GetGain(_currentStrip);
            bool muted = GetMuteState(_currentStrip);
            ShowOverlayVolume(stripName, gain, muted);
        }

        private static string GetStripName(int stripIndex)
        {
            try
            {
                var buffer = new StringBuilder(512);
                string cmd = $"Strip[{stripIndex}].Label";
                int result = VoicemeeterRemote.VBVMR_GetParameterStringA(cmd, buffer, buffer.Capacity);
                if (result == 0)
                {
                    string _name = buffer.ToString();
                    if (!string.IsNullOrWhiteSpace(_name))
                        return _name;

                    var entry = _stripNames.FirstOrDefault(x => x.vmType == 2 && x.index == stripIndex);
                    if (entry != default)
                        return entry.name;
                }
            }
            catch { }

            var fallback = _stripNames.FirstOrDefault(x => x.vmType == 2 && x.index == stripIndex);
            return fallback != default ? fallback.name : $"Strip {stripIndex}";
        }

        private static void MuteStrip(int stripIndex, bool mute)
        {
            try
            {
                string cmd = mute
                    ? $"Strip[{stripIndex}].mute = 1"
                    : $"Strip[{stripIndex}].mute = 0";
                int result = VoicemeeterRemote.VBVMR_SetParametersW(cmd);
                Debug.WriteLine($"MuteStrip: cmd={cmd}, result={result}");
                if (result != 0)
                {
                    Debug.WriteLine($"Failed to set mute: {result}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception setting mute: {ex}");
            }
        }

        private static bool GetMuteState(int stripIndex)
        {
            try
            {
                string param = $"Strip[{stripIndex}].Mute";
                VoicemeeterRemote.VBVMR_IsParametersDirty();
                float value = 0f;
                int result = VoicemeeterRemote.VBVMR_GetParameterFloat(param, ref value);
                if (result == 0)
                {
                    return value > 0.5f;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception getting mute state: {ex}");
            }
            return false;
        }

        private static float GetGain(int stripIndex)
        {
            try
            {
                string param = $"Strip[{stripIndex}].Gain";
                VoicemeeterRemote.VBVMR_IsParametersDirty();
                float value = 0f;
                int result = VoicemeeterRemote.VBVMR_GetParameterFloat(param, ref value);
                if (result == 0)
                {
                    return value;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception getting gain: {ex}");
            }
            return 0f;
        }

        private void ShowOverlayVolume(string name, float gain, bool muted)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => _overlay.ShowVolume(name, gain, muted)));
            }
            else
            {
                _overlay.ShowVolume(name, gain, muted);
            }
        }

        //private static List<(int, string)> getStripNames()
        //{
        //    return new List<(int, string)> { };
        //}

        #region structs, imports, etc
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int VK_VOLUME_MUTE = 0xAD;
        private const int VK_VOLUME_DOWN = 0xAE;
        private const int VK_VOLUME_UP = 0xAF;

        private static IntPtr _hookId = IntPtr.Zero;
        private static HookProc? _hookCallback;

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_hookId == IntPtr.Zero)
            {
                _hookCallback = HookCallback;
                using var curProcess = Process.GetCurrentProcess();
                using var curModule = curProcess.MainModule!;
                IntPtr hMod = GetModuleHandle(curModule.ModuleName);
                _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookCallback, hMod, 0);
            }
        }

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        internal static class VoicemeeterRemote
        {
            [DllImport("VoicemeeterRemote64.dll", CallingConvention = CallingConvention.StdCall)]
            public static extern int VBVMR_Login();

            [DllImport("VoicemeeterRemote64.dll", CallingConvention = CallingConvention.StdCall)]
            public static extern int VBVMR_Logout();

            [DllImport("VoicemeeterRemote64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
            public static extern int VBVMR_GetParameterStringA(string szParamName, StringBuilder value, int length);

            [DllImport("VoicemeeterRemote64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
            public static extern int VBVMR_SetParametersW(string szParameters);
            [DllImport("VoicemeeterRemote64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
            public static extern int VBVMR_GetParameterFloat(string szParamName, ref float value);
            [DllImport("VoicemeeterRemote64.dll", CallingConvention = CallingConvention.StdCall)]
            public static extern int VBVMR_IsParametersDirty();

        }
        #endregion
    }
}

