using System;
using System.Runtime.InteropServices;
using Windows.Graphics.Capture;

namespace nsaygqv0ixdkwb
{
    /// <summary>
    /// Interop glue for creating a GraphicsCaptureItem for a monitor,
    /// for getting the HWND of the current CoreWindow and for excluding
    /// the widget window from screen capture (so the zoom doesn't recurse
    /// into itself).
    /// </summary>
    internal static class CaptureHelper
    {
        private const int MONITOR_DEFAULTTONEAREST = 2;
        private const uint WDA_NONE = 0x0;
        private const uint WDA_MONITOR = 0x1;
        private const uint WDA_EXCLUDEFROMCAPTURE = 0x11;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);

        /// <summary>
        /// Returns non-zero high bit (0x8000) while the key is currently down.
        /// From a UWP AppContainer this only works when the package declares the
        /// inputForegroundObservation restricted capability. Without it, the call
        /// returns 0 whenever the foreground window belongs to another process.
        /// </summary>
        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT lpPoint);

        public static bool TryGetCursorPos(out int x, out int y)
        {
            if (GetCursorPos(out var p)) { x = p.X; y = p.Y; return true; }
            x = 0; y = 0; return false;
        }

        private const uint GA_PARENT = 1;
        private const uint GA_ROOT = 2;
        private const uint GA_ROOTOWNER = 3;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetForegroundWindow();

        /// <summary>Walks up to the top-level root window (GA_ROOT).</summary>
        public static IntPtr GetRootHwnd(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return IntPtr.Zero;
            IntPtr root = GetAncestor(hwnd, GA_ROOT);
            return root != IntPtr.Zero ? root : hwnd;
        }

        /// <summary>Walks up to the root owner window (GA_ROOTOWNER).</summary>
        public static IntPtr GetRootOwnerHwnd(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return IntPtr.Zero;
            IntPtr root = GetAncestor(hwnd, GA_ROOTOWNER);
            return root != IntPtr.Zero ? root : hwnd;
        }

        [ComImport, Guid("3E68D4BD-7135-4D10-8018-9FB6D9F33FA1"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IInitializeWithWindow
        {
            void Initialize(IntPtr hwnd);
        }

        /// <summary>
        /// Desktop-style host objects like XboxGameBar widgets need the picker to be
        /// explicitly bound to an owner HWND via IInitializeWithWindow, otherwise
        /// PickSingleItemAsync silently does nothing.
        /// </summary>
        public static bool TryInitializeWithWindow(object winRtObject, IntPtr hwnd)
        {
            if (winRtObject == null || hwnd == IntPtr.Zero) return false;
            try
            {
                var init = (IInitializeWithWindow)winRtObject;
                init.Initialize(hwnd);
                return true;
            }
            catch
            {
                return false;
            }
        }

        [ComImport, Guid("45D64A29-A63E-4CB6-B498-5781D298CB4F"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ICoreWindowInterop
        {
            IntPtr WindowHandle { get; }
            bool MessageHandled { set; }
        }

        [ComImport, Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IGraphicsCaptureItemInterop
        {
            IntPtr CreateForWindow([In] IntPtr window, [In] ref Guid iid);
            IntPtr CreateForMonitor([In] IntPtr monitor, [In] ref Guid iid);
        }

        [DllImport("api-ms-win-core-winrt-l1-1-0.dll", CharSet = CharSet.Unicode,
                   ExactSpelling = true, PreserveSig = false)]
        private static extern IntPtr RoGetActivationFactory(
            [MarshalAs(UnmanagedType.HString)] string activatableClassId,
            [In] ref Guid iid);

        private static readonly Guid IID_IGraphicsCaptureItemInterop =
            new Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");

        private static readonly Guid IID_IGraphicsCaptureItem =
            new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");

        /// <summary>
        /// Returns the native HWND of a CoreWindow.
        /// </summary>
        public static IntPtr GetCoreWindowHwnd(Windows.UI.Core.CoreWindow coreWindow)
        {
            if (coreWindow == null) return IntPtr.Zero;
            var interop = (ICoreWindowInterop)(object)coreWindow;
            return interop.WindowHandle;
        }

        /// <summary>
        /// Creates a GraphicsCaptureItem targeting the monitor that currently
        /// contains the given window.
        /// </summary>
        public static GraphicsCaptureItem CreateItemForMonitor(IntPtr hwnd)
        {
            IntPtr hmon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (hmon == IntPtr.Zero) return null;

            Guid interopIid = IID_IGraphicsCaptureItemInterop;
            IntPtr factoryPtr = RoGetActivationFactory(
                "Windows.Graphics.Capture.GraphicsCaptureItem", ref interopIid);
            if (factoryPtr == IntPtr.Zero) return null;

            IGraphicsCaptureItemInterop interop = null;
            try
            {
                interop = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factoryPtr);
            }
            finally
            {
                Marshal.Release(factoryPtr);
            }

            if (interop == null) return null;

            Guid itemIid = IID_IGraphicsCaptureItem;
            IntPtr rawPtr = interop.CreateForMonitor(hmon, ref itemIid);
            if (rawPtr == IntPtr.Zero) return null;

            try
            {
                return Marshal.GetObjectForIUnknown(rawPtr) as GraphicsCaptureItem;
            }
            finally
            {
                Marshal.Release(rawPtr);
            }
        }

        /// <summary>
        /// Queries the monitor rect for the monitor that contains the given HWND.
        /// Returns false if the call fails.
        /// </summary>
        public static bool TryGetMonitorRect(IntPtr hwnd, out RECT monitorRect)
        {
            monitorRect = default;
            IntPtr hmon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (hmon == IntPtr.Zero) return false;

            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(hmon, ref mi)) return false;

            monitorRect = mi.rcMonitor;
            return true;
        }

        /// <summary>
        /// Prevents the given window from being captured by screen-capture APIs.
        /// WDA must be applied to the top-level window; a child HWND (like a
        /// CoreWindow inside an ApplicationFrameWindow) is rejected silently.
        /// We therefore walk up to the root HWND first.
        /// On Windows 10 2004+ we use WDA_EXCLUDEFROMCAPTURE (invisible to capture).
        /// On older builds we fall back to WDA_MONITOR (shows black in capture).
        /// </summary>
        public static bool ExcludeFromCapture(IntPtr hwnd, bool exclude)
        {
            if (hwnd == IntPtr.Zero) return false;
            IntPtr top = GetRootHwnd(hwnd);

            if (!exclude)
            {
                SetWindowDisplayAffinity(top, WDA_NONE);
                if (top != hwnd) SetWindowDisplayAffinity(hwnd, WDA_NONE);
                return true;
            }

            // Prefer WDA_EXCLUDEFROMCAPTURE (Win10 2004+) — widget is absent from capture.
            if (SetWindowDisplayAffinity(top, WDA_EXCLUDEFROMCAPTURE)) return true;
            if (top != hwnd && SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)) return true;

            // Fallback: WDA_MONITOR renders widget as solid black in capture — no recursion.
            if (SetWindowDisplayAffinity(top, WDA_MONITOR)) return true;
            if (top != hwnd && SetWindowDisplayAffinity(hwnd, WDA_MONITOR)) return true;

            return false;
        }
    }
}
