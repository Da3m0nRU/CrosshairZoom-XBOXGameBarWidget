using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Gaming.XboxGameBar;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace nsaygqv0ixdkwb
{
    public sealed partial class MainPage : Page
    {
        private XboxGameBarWidget _widget;

        // DirectX / Win2D
        private CanvasDevice _canvasDevice;
        private CanvasSwapChain _swapChain;
        private CanvasRenderTarget _frameBuffer;

        // Capture
        private GraphicsCaptureItem _captureItem;
        private Direct3D11CaptureFramePool _framePool;
        private GraphicsCaptureSession _captureSession;
        private SizeInt32 _captureSize;

        // Render loop
        private Thread _renderThread;
        private volatile bool _isRunning;
        private readonly object _frameLock = new object();
        private Direct3D11CaptureFrame _latestFrame;
        private IntPtr _widgetHwnd = IntPtr.Zero;

        // Primary-button behavior: pick screen vs retry
        private enum PrimaryAction { None, SelectScreen, Retry }
        private PrimaryAction _primaryAction = PrimaryAction.None;

        public MainPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            _widget = e.Parameter as XboxGameBarWidget;

            if (_widget == null)
            {
                ShowInfo("Zoom widget\n\nOpen this from the Xbox Game Bar (Win+G).",
                    PrimaryAction.None, null);
                return;
            }

            try { _widget.SettingsClicked += OnSettingsClicked; } catch { }
            try { Window.Current.SizeChanged += OnWindowSizeChanged; } catch { }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            Shutdown();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (_widget == null) return;
            InitializeCoreGraphics();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            Shutdown();
        }

        private void InitializeCoreGraphics()
        {
            // Step 1: HWND
            try
            {
                _widgetHwnd = CaptureHelper.GetCoreWindowHwnd(Window.Current.CoreWindow);
            }
            catch (Exception ex)
            {
                ShowInfo("Failed to get window handle:\n" + ex.Message,
                    PrimaryAction.Retry, "Retry");
                return;
            }

            try { CaptureHelper.ExcludeFromCapture(_widgetHwnd, true); } catch { }

            // Step 2: graphics device + swap chain
            try
            {
                InitializeGraphics();
            }
            catch (Exception ex)
            {
                ShowInfo("Graphics init failed:\n" + ex.Message,
                    PrimaryAction.Retry, "Retry");
                return;
            }

            // Step 3: on Windows 11 try a silent monitor capture first.
            // On Windows 10 this path will fail and we'll ask the user to pick.
            if (TryStartMonitorCaptureSilently())
            {
                HideInfo();
                StartRenderThread();
                return;
            }

            // Step 4: ask the user to pick a screen through the system picker.
            // This requires a real user gesture on Windows 10, so we wait for a click.
            ShowInfo(
                "Click \"Select screen\" and choose the monitor you want to magnify. " +
                "Windows will ask once — after that the widget starts automatically.",
                PrimaryAction.SelectScreen, "Select screen");
        }

        private async void PrimaryButton_Click(object sender, RoutedEventArgs e)
        {
            switch (_primaryAction)
            {
                case PrimaryAction.SelectScreen:
                    await PickAndStartAsync();
                    break;
                case PrimaryAction.Retry:
                    Shutdown(keepWidget: true);
                    InitializeCoreGraphics();
                    break;
            }
        }

        /// <summary>
        /// Tries to create a capture item for the current monitor without a picker.
        /// Succeeds only on OSes/capabilities that allow programmatic monitor capture
        /// (Windows 11 with graphicsCaptureProgrammatic + user consent).
        /// </summary>
        private bool TryStartMonitorCaptureSilently()
        {
            // On Windows 10 CreateForMonitor throws E_ACCESSDENIED, so we only try
            // when the runtime signals support via GraphicsCaptureAccess.
            if (!ApiInformation.IsTypePresent("Windows.Graphics.Capture.GraphicsCaptureAccess"))
            {
                return false;
            }

            GraphicsCaptureItem item = null;
            try { item = CaptureHelper.CreateItemForMonitor(_widgetHwnd); }
            catch { item = null; }

            if (item == null) return false;

            try
            {
                StartSessionWithItem(item);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task PickAndStartAsync()
        {
            if (!GraphicsCaptureSession.IsSupported())
            {
                ShowInfo("Screen capture is not supported on this system.",
                    PrimaryAction.None, null);
                return;
            }

            // Game Bar widgets need the picker parented to a real top-level HWND.
            // CoreWindow HWND doesn't work (it's a child). We try root / owner HWNDs
            // in order until the picker returns an item or throws.
            var candidates = new System.Collections.Generic.List<IntPtr>();
            IntPtr coreHwnd = _widgetHwnd;
            IntPtr rootHwnd = CaptureHelper.GetRootHwnd(coreHwnd);
            IntPtr ownerHwnd = CaptureHelper.GetRootOwnerHwnd(coreHwnd);
            IntPtr foregroundHwnd = CaptureHelper.GetForegroundWindow();

            if (rootHwnd != IntPtr.Zero) candidates.Add(rootHwnd);
            if (ownerHwnd != IntPtr.Zero && !candidates.Contains(ownerHwnd)) candidates.Add(ownerHwnd);
            if (foregroundHwnd != IntPtr.Zero && !candidates.Contains(foregroundHwnd)) candidates.Add(foregroundHwnd);
            if (!candidates.Contains(coreHwnd)) candidates.Add(coreHwnd);

            GraphicsCaptureItem item = null;
            Exception lastEx = null;
            foreach (var hwnd in candidates)
            {
                if (item != null) break;
                try
                {
                    var picker = new GraphicsCapturePicker();
                    CaptureHelper.TryInitializeWithWindow(picker, hwnd);
                    item = await picker.PickSingleItemAsync();
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                }
            }

            if (item == null)
            {
                string extra = lastEx != null ? "\n\n" + lastEx.Message : "";
                ShowInfo(
                    "No screen selected. PIN this widget first (push-pin icon in the header), " +
                    "close Game Bar, then click Select screen again." + extra,
                    PrimaryAction.SelectScreen, "Select screen");
                return;
            }

            try
            {
                StartSessionWithItem(item);
                HideInfo();
                StartRenderThread();
                EnsureLockedCenterInitialized();
            }
            catch (Exception ex)
            {
                ShowInfo("Failed to start capture:\n" + ex.Message,
                    PrimaryAction.Retry, "Retry");
            }
        }

        private void StartSessionWithItem(GraphicsCaptureItem item)
        {
            _captureItem = item;
            _captureSize = _captureItem.Size;

            _framePool = Direct3D11CaptureFramePool.Create(
                _canvasDevice,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                2,
                _captureSize);

            _framePool.FrameArrived += OnFrameArrived;
            _captureItem.Closed += OnCaptureItemClosed;

            _captureSession = _framePool.CreateCaptureSession(_captureItem);

            try
            {
                if (ApiInformation.IsPropertyPresent(
                    typeof(GraphicsCaptureSession).FullName, "IsCursorCaptureEnabled"))
                {
                    _captureSession.IsCursorCaptureEnabled = true;
                }
            }
            catch { }

            try
            {
                if (ApiInformation.IsPropertyPresent(
                    typeof(GraphicsCaptureSession).FullName, "IsBorderRequired"))
                {
                    _captureSession.IsBorderRequired = false;
                }
            }
            catch { }

            // Re-apply exclusion right before capture starts. The widget HWND may
            // have changed when the user pinned the widget, and WDA must sit on
            // the CURRENT top-level HWND. Re-fetch and re-apply.
            try
            {
                _widgetHwnd = CaptureHelper.GetCoreWindowHwnd(Window.Current.CoreWindow);
                CaptureHelper.ExcludeFromCapture(_widgetHwnd, true);
            }
            catch { }

            _captureSession.StartCapture();
        }

        private void ShowInfo(string message, PrimaryAction action, string buttonText)
        {
            if (InfoPanel == null) return;
            InfoText.Text = message;
            _primaryAction = action;
            if (action == PrimaryAction.None || string.IsNullOrEmpty(buttonText))
            {
                PrimaryButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                PrimaryButton.Content = buttonText;
                PrimaryButton.Visibility = Visibility.Visible;
            }
            InfoPanel.Visibility = Visibility.Visible;
        }

        private void HideInfo()
        {
            if (InfoPanel == null) return;
            InfoPanel.Visibility = Visibility.Collapsed;
        }

        private void Shutdown(bool keepWidget = false)
        {
            _isRunning = false;

            if (!keepWidget)
            {
                try { Window.Current.SizeChanged -= OnWindowSizeChanged; } catch { }
                if (_widget != null)
                {
                    try { _widget.SettingsClicked -= OnSettingsClicked; } catch { }
                    _widget = null;
                }
            }

            try { _renderThread?.Join(500); } catch { }
            _renderThread = null;

            try { _captureSession?.Dispose(); } catch { }
            _captureSession = null;

            try
            {
                if (_framePool != null)
                {
                    _framePool.FrameArrived -= OnFrameArrived;
                    _framePool.Dispose();
                }
            }
            catch { }
            _framePool = null;

            if (_captureItem != null)
            {
                try { _captureItem.Closed -= OnCaptureItemClosed; } catch { }
                _captureItem = null;
            }

            lock (_frameLock)
            {
                try { _latestFrame?.Dispose(); } catch { }
                _latestFrame = null;
            }

            try { _swapChain?.Dispose(); } catch { }
            _swapChain = null;

            try { _frameBuffer?.Dispose(); } catch { }
            _frameBuffer = null;

            try { _canvasDevice?.Dispose(); } catch { }
            _canvasDevice = null;

            if (!keepWidget && _widgetHwnd != IntPtr.Zero)
            {
                try { CaptureHelper.ExcludeFromCapture(_widgetHwnd, false); } catch { }
                _widgetHwnd = IntPtr.Zero;
            }
        }

        private async void OnSettingsClicked(XboxGameBarWidget sender, object args)
        {
            try { await sender.ActivateSettingsAsync(); } catch { }
        }

        private void OnWindowSizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            if (_swapChain == null) return;
            try
            {
                float w = Math.Max((float)e.Size.Width, 64);
                float h = Math.Max((float)e.Size.Height, 64);
                _swapChain.ResizeBuffers(w, h, 96);
            }
            catch { }
        }

        private void InitializeGraphics()
        {
            try
            {
                _canvasDevice = CanvasDevice.GetSharedDevice(false);
            }
            catch
            {
                _canvasDevice = new CanvasDevice();
            }

            var bounds = Window.Current.CoreWindow.Bounds;
            float width = Math.Max((float)bounds.Width, 64);
            float height = Math.Max((float)bounds.Height, 64);

            _swapChain = new CanvasSwapChain(_canvasDevice, width, height, 96);
            ZoomCanvas.SwapChain = _swapChain;
        }

        private void OnCaptureItemClosed(GraphicsCaptureItem sender, object args)
        {
            _isRunning = false;
        }

        private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            var frame = sender.TryGetNextFrame();
            if (frame == null) return;

            if (frame.ContentSize.Width != _captureSize.Width ||
                frame.ContentSize.Height != _captureSize.Height)
            {
                _captureSize = frame.ContentSize;
                try
                {
                    sender.Recreate(
                        _canvasDevice,
                        DirectXPixelFormat.B8G8R8A8UIntNormalized,
                        2,
                        _captureSize);
                }
                catch { }
            }

            lock (_frameLock)
            {
                _latestFrame?.Dispose();
                _latestFrame = frame;
            }
        }

        private void StartRenderThread()
        {
            _isRunning = true;
            _renderThread = new Thread(RenderLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal,
                Name = "ZoomRender"
            };
            _renderThread.Start();
        }

        // Hotkey state (evaluated in the render thread)
        private bool _prevHotkeyDown;
        private bool _hotkeyToggleState;

        // Adaptive / frame-skip state
        private int _frameSkipCounter;
        private double _emaFrameMs;
        private long _adaptiveLastTick;

        // Last seen command sequences from settings widget (cross-process).
        private int _lastResizeSeq = -1;
        private int _lastCenterSeq = -1;
        private int _lastLockHereSeq = -1;

        private void RenderLoop()
        {
            while (_isRunning)
            {
                int fpsCap = ZoomSettings.Instance.MaxFps;
                int frameBudgetMs = fpsCap > 0 ? Math.Max(1, 1000 / fpsCap) : 8;
                long startTicks = Environment.TickCount;

                try
                {
                    UpdateHotkey();
                    HandlePendingCommands();
                    MaybeApplyAdaptive(startTicks);

                    int skip = Math.Max(0, ZoomSettings.Instance.FrameSkip);
                    if (skip > 0 && _frameSkipCounter < skip)
                    {
                        _frameSkipCounter++;
                    }
                    else
                    {
                        _frameSkipCounter = 0;
                        DrawOneFrame();
                    }
                }
                catch
                {
                    // Swallow — one bad frame shouldn't kill the loop.
                }

                int elapsed = Environment.TickCount - (int)startTicks;
                // EMA of per-iteration cost for the adaptive controller.
                _emaFrameMs = _emaFrameMs <= 0 ? elapsed : (_emaFrameMs * 0.9 + elapsed * 0.1);

                int sleep = frameBudgetMs - elapsed;
                if (sleep > 0) Thread.Sleep(sleep);
                else Thread.Yield();
            }
        }

        /// <summary>
        /// Reads cross-process command counters written by the settings widget and
        /// performs the corresponding Game Bar widget action (resize/center).
        /// </summary>
        private void HandlePendingCommands()
        {
            if (_widget == null) return;
            var s = ZoomSettings.Instance;

            int resizeSeq = s.RequestedWidgetSizeSeq;
            if (resizeSeq != _lastResizeSeq)
            {
                _lastResizeSeq = resizeSeq;
                double size = s.RequestedWidgetSize;
                if (size > 0)
                {
                    // Fire-and-forget; must run on UI thread.
                    _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                    {
                        try
                        {
                            await _widget.TryResizeWindowAsync(new Size(size, size));
                        }
                        catch { }
                    });
                }
            }

            int centerSeq = s.RequestedCenterSeq;
            if (centerSeq != _lastCenterSeq)
            {
                _lastCenterSeq = centerSeq;
                _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    try
                    {
                        // CenterWindowAsync was added to XboxGameBarWidget after our
                        // SDK version; call it reflectively when present, no-op otherwise.
                        var mi = _widget.GetType().GetMethod(
                            "CenterWindowAsync",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                            null, Type.EmptyTypes, null);
                        if (mi != null)
                        {
                            var action = mi.Invoke(_widget, null) as Windows.Foundation.IAsyncAction;
                            if (action != null) await action;
                        }
                    }
                    catch { }
                });
            }

            int lockHereSeq = s.RequestedLockHereSeq;
            if (lockHereSeq != _lastLockHereSeq)
            {
                _lastLockHereSeq = lockHereSeq;
                // Snapshot the current cursor position in monitor-local coords as
                // the new lock target, and turn Lock on.
                if (CaptureHelper.TryGetCursorPos(out int cx, out int cy) &&
                    CaptureHelper.TryGetMonitorRect(_widgetHwnd, out var monRect))
                {
                    double lx = cx - monRect.Left;
                    double ly = cy - monRect.Top;
                    if (lx >= 0 && ly >= 0 &&
                        lx <= (monRect.Right - monRect.Left) &&
                        ly <= (monRect.Bottom - monRect.Top))
                    {
                        s.LockedCenterX = lx;
                        s.LockedCenterY = ly;
                        s.LockZoomArea = true;
                    }
                }
            }
        }

        /// <summary>
        /// If adaptive performance is on, adjusts FrameSkip based on how long render
        /// cycles actually take relative to the configured budget. Runs at most once
        /// per 500ms to avoid oscillation.
        /// </summary>
        private void MaybeApplyAdaptive(long nowTicks)
        {
            var s = ZoomSettings.Instance;
            if (!s.AdaptivePerformance) return;
            if (nowTicks - _adaptiveLastTick < 500) return;
            _adaptiveLastTick = nowTicks;

            int fpsCap = s.MaxFps;
            int budget = fpsCap > 0 ? Math.Max(1, 1000 / fpsCap) : 8;

            int skip = s.FrameSkip;
            // If we're spending more than ~1.5x the budget, drop a frame out of every N.
            if (_emaFrameMs > budget * 1.5 && skip < 3)
            {
                s.FrameSkip = skip + 1;
            }
            else if (_emaFrameMs < budget * 0.6 && skip > 0)
            {
                s.FrameSkip = skip - 1;
            }
        }

        private void UpdateHotkey()
        {
            var s = ZoomSettings.Instance;
            if (!s.HotkeyEnabled) { _prevHotkeyDown = false; return; }

            int vk = s.HotkeyVirtualKey;
            if (vk <= 0 || vk > 0xFF) { _prevHotkeyDown = false; return; }

            bool down = false;
            try { down = (CaptureHelper.GetAsyncKeyState(vk) & 0x8000) != 0; } catch { }

            if (s.HotkeyMode == 1 && down && !_prevHotkeyDown)
            {
                _hotkeyToggleState = !_hotkeyToggleState;
            }

            _prevHotkeyDown = down;
        }

        private bool HotkeyActive()
        {
            var s = ZoomSettings.Instance;
            if (!s.HotkeyEnabled) return true;
            if (s.HotkeyMode == 1) return _hotkeyToggleState;
            return _prevHotkeyDown;
        }

        private void DrawOneFrame()
        {
            if (_swapChain == null) return;

            Direct3D11CaptureFrame frame;
            lock (_frameLock)
            {
                frame = _latestFrame;
            }

            var settings = ZoomSettings.Instance;
            float swapW = (float)(_swapChain.Size.Width > 0 ? _swapChain.Size.Width : 1);
            float swapH = (float)(_swapChain.Size.Height > 0 ? _swapChain.Size.Height : 1);

            using (var ds = _swapChain.CreateDrawingSession(Colors.Transparent))
            {
                bool gateOpen = HotkeyActive();

                if (frame == null || !settings.Enabled || !gateOpen)
                {
                    // Fully transparent when not rendering — no fill, no border.
                    _swapChain.Present();
                    return;
                }

                using (var bitmap = CanvasBitmap.CreateFromDirect3D11Surface(_canvasDevice, frame.Surface))
                {
                    Rect widgetScreenRect = GetWidgetScreenRect();

                    // Mask the widget area in the captured frame so the zoom can
                    // never include itself — eliminates the recursion tunnel.
                    var masked = GetOrCreateFrameBuffer(bitmap.SizeInPixels.Width, bitmap.SizeInPixels.Height);
                    using (var bds = masked.CreateDrawingSession())
                    {
                        bds.DrawImage(bitmap);
                        const double pad = 2.0;
                        bds.FillRectangle(
                            (float)Math.Max(0, widgetScreenRect.Left - pad),
                            (float)Math.Max(0, widgetScreenRect.Top - pad),
                            (float)(widgetScreenRect.Width + pad * 2),
                            (float)(widgetScreenRect.Height + pad * 2),
                            Colors.Black);
                    }

                    Rect sourceRect = ComputeSourceRect(widgetScreenRect, new Size(masked.Size.Width, masked.Size.Height), settings);
                    Rect destRect = new Rect(0, 0, swapW, swapH);

                    if (settings.Circular)
                    {
                        float cx = swapW * 0.5f;
                        float cy = swapH * 0.5f;
                        float r = Math.Min(swapW, swapH) * 0.5f;

                        var clip = CanvasGeometry.CreateCircle(_canvasDevice, cx, cy, r);
                        using (ds.CreateLayer(1f, clip))
                        {
                            ds.DrawImage(masked, destRect, sourceRect,
                                1f, CanvasImageInterpolation.Linear);
                        }
                    }
                    else
                    {
                        ds.DrawImage(masked, destRect, sourceRect,
                            1f, CanvasImageInterpolation.Linear);
                    }
                }

                DrawCrosshair(ds, swapW, swapH, settings);
            }

            _swapChain.Present();
        }

        private void DrawCrosshair(CanvasDrawingSession ds, float w, float h, ZoomSettings settings)
        {
            if (!settings.CrosshairEnabled) return;

            float cx = w * 0.5f;
            float cy = h * 0.5f;
            float size = settings.CrosshairSize * 0.5f;
            float thick = settings.CrosshairThickness;
            float gap = settings.CrosshairGap;

            uint argb = settings.CrosshairColor;
            var color = Color.FromArgb(
                (byte)(argb >> 24),
                (byte)(argb >> 16),
                (byte)(argb >> 8),
                (byte)argb);

            var style = (ZoomSettings.CrosshairStyleKind)settings.CrosshairStyle;

            switch (style)
            {
                case ZoomSettings.CrosshairStyleKind.Dot:
                    ds.FillCircle(cx, cy, thick * 1.5f, color);
                    break;

                case ZoomSettings.CrosshairStyleKind.Cross:
                    ds.DrawLine(cx - size, cy, cx + size, cy, color, thick);
                    ds.DrawLine(cx, cy - size, cx, cy + size, color, thick);
                    break;

                case ZoomSettings.CrosshairStyleKind.Plus:
                    // Horizontal
                    ds.DrawLine(cx - size, cy, cx - gap, cy, color, thick);
                    ds.DrawLine(cx + gap, cy, cx + size, cy, color, thick);
                    // Vertical
                    ds.DrawLine(cx, cy - size, cx, cy - gap, color, thick);
                    ds.DrawLine(cx, cy + gap, cx, cy + size, color, thick);
                    break;

                case ZoomSettings.CrosshairStyleKind.Circle:
                    ds.DrawCircle(cx, cy, size, color, thick);
                    break;

                case ZoomSettings.CrosshairStyleKind.CircleDot:
                    ds.DrawCircle(cx, cy, size, color, thick);
                    ds.FillCircle(cx, cy, thick * 1.5f, color);
                    break;

                case ZoomSettings.CrosshairStyleKind.TShape:
                    // Horizontal full
                    ds.DrawLine(cx - size, cy, cx + size, cy, color, thick);
                    // Vertical bottom only
                    ds.DrawLine(cx, cy, cx, cy + size, color, thick);
                    break;

                case ZoomSettings.CrosshairStyleKind.Chevron:
                    // V-shape pointing down
                    ds.DrawLine(cx - size * 0.6f, cy - size * 0.4f, cx, cy + size * 0.2f, color, thick);
                    ds.DrawLine(cx + size * 0.6f, cy - size * 0.4f, cx, cy + size * 0.2f, color, thick);
                    break;

                default:
                    break;
            }
        }

        private CanvasRenderTarget GetOrCreateFrameBuffer(uint widthPx, uint heightPx)
        {
            if (_frameBuffer != null &&
                (uint)_frameBuffer.SizeInPixels.Width == widthPx &&
                (uint)_frameBuffer.SizeInPixels.Height == heightPx)
            {
                return _frameBuffer;
            }
            try { _frameBuffer?.Dispose(); } catch { }
            _frameBuffer = new CanvasRenderTarget(
                _canvasDevice, widthPx, heightPx, 96,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                Microsoft.Graphics.Canvas.CanvasAlphaMode.Premultiplied);
            return _frameBuffer;
        }

        private Rect GetWidgetScreenRect()
        {
            if (CaptureHelper.TryGetMonitorRect(_widgetHwnd, out var monRect) &&
                CaptureHelper.GetWindowRect(_widgetHwnd, out var winRect))
            {
                return new Rect(
                    winRect.Left - monRect.Left,
                    winRect.Top - monRect.Top,
                    Math.Max(1, winRect.Width),
                    Math.Max(1, winRect.Height));
            }
            return new Rect(0, 0, 400, 400);
        }

        /// <summary>
        /// On first start, seed LockedCenter to the monitor centre so that when
        /// the user eventually turns Lock Zoom Area on, they get a sensible default.
        /// Does NOT force Lock on — default behaviour is cursor-following.
        /// </summary>
        private void EnsureLockedCenterInitialized()
        {
            var s = ZoomSettings.Instance;
            if (s.LockedCenterX <= 0 && s.LockedCenterY <= 0)
            {
                if (CaptureHelper.TryGetMonitorRect(_widgetHwnd, out var monRect))
                {
                    s.LockedCenterX = (monRect.Right - monRect.Left) * 0.5;
                    s.LockedCenterY = (monRect.Bottom - monRect.Top) * 0.5;
                }
            }
        }

        private Rect ComputeSourceRect(Rect widgetScreenRect, Size captureSize, ZoomSettings settings)
        {
            double mag = Math.Max(1.0, settings.Magnification);
            double srcW = widgetScreenRect.Width / mag;
            double srcH = widgetScreenRect.Height / mag;

            double centerX, centerY;
            if (settings.LockZoomArea)
            {
                if (settings.LockedCenterX > 0 || settings.LockedCenterY > 0)
                {
                    centerX = settings.LockedCenterX;
                    centerY = settings.LockedCenterY;
                }
                else
                {
                    // Lock is on but no explicit point — use monitor centre.
                    centerX = captureSize.Width * 0.5;
                    centerY = captureSize.Height * 0.5;
                }
            }
            else
            {
                // Follow the mouse cursor. This is the classic magnifier behavior.
                // Cursor coords need to be converted to monitor-local (same space as captureSize).
                if (CaptureHelper.TryGetCursorPos(out int cx, out int cy) &&
                    CaptureHelper.TryGetMonitorRect(_widgetHwnd, out var monRect))
                {
                    centerX = cx - monRect.Left;
                    centerY = cy - monRect.Top;

                    // If the cursor is on a different monitor, fall back to monitor centre.
                    if (centerX < 0 || centerY < 0 ||
                        centerX > captureSize.Width || centerY > captureSize.Height)
                    {
                        centerX = captureSize.Width * 0.5;
                        centerY = captureSize.Height * 0.5;
                    }
                }
                else
                {
                    centerX = captureSize.Width * 0.5;
                    centerY = captureSize.Height * 0.5;
                }
            }

            double left = centerX - srcW * 0.5;
            double top = centerY - srcH * 0.5;

            if (left < 0) left = 0;
            if (top < 0) top = 0;
            if (left + srcW > captureSize.Width) left = captureSize.Width - srcW;
            if (top + srcH > captureSize.Height) top = captureSize.Height - srcH;
            if (left < 0) left = 0;
            if (top < 0) top = 0;
            if (srcW > captureSize.Width) srcW = captureSize.Width;
            if (srcH > captureSize.Height) srcH = captureSize.Height;

            return new Rect(left, top, srcW, srcH);
        }
    }
}
