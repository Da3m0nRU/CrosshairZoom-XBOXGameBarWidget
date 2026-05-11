using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.Storage;

namespace nsaygqv0ixdkwb
{
    /// <summary>
    /// Persisted settings for the zoom widget. Writes to ApplicationData.LocalSettings
    /// so that both the main widget and the settings widget see the same values.
    /// </summary>
    public sealed class ZoomSettings : INotifyPropertyChanged
    {
        public static ZoomSettings Instance { get; } = new ZoomSettings();

        private readonly ApplicationDataContainer _store = ApplicationData.Current.LocalSettings;

        public event PropertyChangedEventHandler PropertyChanged;

        private T Get<T>(string key, T fallback)
        {
            try
            {
                if (_store.Values.TryGetValue(key, out object raw) && raw is T v) return v;
            }
            catch { }
            return fallback;
        }

        private void Set<T>(string key, T value, [CallerMemberName] string name = null)
        {
            try
            {
                _store.Values[key] = value;
            }
            catch { }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public bool Enabled
        {
            get => Get("enabled", true);
            set => Set("enabled", value);
        }

        /// <summary>Zoom level. 1..8, default 2.</summary>
        public double Magnification
        {
            get => Get("magnification", 2.0);
            set => Set("magnification", Math.Max(1.0, Math.Min(8.0, value)));
        }

        /// <summary>True = circular clip, false = rectangular.</summary>
        public bool Circular
        {
            get => Get("circular", true);
            set => Set("circular", value);
        }

        /// <summary>If true, the zoom source area stays fixed on screen.</summary>
        public bool LockZoomArea
        {
            get => Get("lockArea", false);
            set => Set("lockArea", value);
        }

        /// <summary>Locked source center X in screen pixels.</summary>
        public double LockedCenterX
        {
            get => Get("lockedCenterX", 0.0);
            set => Set("lockedCenterX", value);
        }

        /// <summary>Locked source center Y in screen pixels.</summary>
        public double LockedCenterY
        {
            get => Get("lockedCenterY", 0.0);
            set => Set("lockedCenterY", value);
        }

        /// <summary>Max FPS. Fixed at 360 — rendering stays as fast as the monitor can show.</summary>
        public int MaxFps => 360;

        /// <summary>
        /// If true, zoom is only rendered while the hotkey is pressed (Hold mode)
        /// or toggled on (Toggle mode). If false, hotkey is ignored.
        /// </summary>
        public bool HotkeyEnabled
        {
            get => Get("hotkeyEnabled", false);
            set => Set("hotkeyEnabled", value);
        }

        /// <summary>Virtual-key code for the hotkey. Default: VK_MENU (ALT) = 0x12.</summary>
        public int HotkeyVirtualKey
        {
            get => Get("hotkeyVk", 0x12);
            set => Set("hotkeyVk", value);
        }

        /// <summary>0 = Hold (default), 1 = Toggle.</summary>
        public int HotkeyMode
        {
            get => Get("hotkeyMode", 0);
            set => Set("hotkeyMode", Math.Max(0, Math.Min(1, value)));
        }

        /// <summary>Render only every (N+1)-th frame. 0 = render every frame.</summary>
        public int FrameSkip
        {
            get => Get("frameSkip", 0);
            set => Set("frameSkip", Math.Max(0, Math.Min(10, value)));
        }

        /// <summary>Automatically adjusts MaxFps/FrameSkip based on render cost.</summary>
        public bool AdaptivePerformance
        {
            get => Get("adaptivePerf", false);
            set => Set("adaptivePerf", value);
        }

        /// <summary>Requested widget window size in DIPs. 0 = no pending request.</summary>
        public double RequestedWidgetSize
        {
            get => Get("reqWidgetSize", 0.0);
            set => Set("reqWidgetSize", value);
        }

        /// <summary>Monotonic counter incremented each time a size request is placed.</summary>
        public int RequestedWidgetSizeSeq
        {
            get => Get("reqWidgetSizeSeq", 0);
            set => Set("reqWidgetSizeSeq", value);
        }

        /// <summary>Counter incremented to request that the widget centers itself.</summary>
        public int RequestedCenterSeq
        {
            get => Get("reqCenterSeq", 0);
            set => Set("reqCenterSeq", value);
        }

        /// <summary>Counter incremented to request "lock to current cursor position".</summary>
        public int RequestedLockHereSeq
        {
            get => Get("reqLockHereSeq", 0);
            set => Set("reqLockHereSeq", value);
        }

        // ---- Crosshair ----

        public enum CrosshairStyleKind
        {
            None = 0,
            Dot = 1,
            Cross = 2,
            Plus = 3,          // cross with a centre gap
            Circle = 4,
            CircleDot = 5,
            TShape = 6,
            Chevron = 7,
        }

        public bool CrosshairEnabled
        {
            get => Get("crosshairEnabled", false);
            set => Set("crosshairEnabled", value);
        }

        public int CrosshairStyle
        {
            get => Get("crosshairStyle", (int)CrosshairStyleKind.Cross);
            set => Set("crosshairStyle", Math.Max(0, Math.Min(7, value)));
        }

        /// <summary>Packed ARGB. Default opaque green.</summary>
        public uint CrosshairColor
        {
            get
            {
                object raw;
                try { if (_store.Values.TryGetValue("crosshairColor", out raw) && raw is uint u) return u; } catch { }
                return 0xFF00FF00u;
            }
            set => Set("crosshairColor", value);
        }

        /// <summary>Size in pixels. 4..200.</summary>
        public int CrosshairSize
        {
            get => Get("crosshairSize", 24);
            set => Set("crosshairSize", Math.Max(4, Math.Min(200, value)));
        }

        /// <summary>Line thickness in pixels. 1..10.</summary>
        public int CrosshairThickness
        {
            get => Get("crosshairThickness", 2);
            set => Set("crosshairThickness", Math.Max(1, Math.Min(10, value)));
        }

        /// <summary>Gap in pixels around the centre (for Plus / CircleDot).</summary>
        public int CrosshairGap
        {
            get => Get("crosshairGap", 4);
            set => Set("crosshairGap", Math.Max(0, Math.Min(50, value)));
        }
    }
}
