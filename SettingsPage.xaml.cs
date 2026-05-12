using System;
using Microsoft.Gaming.XboxGameBar;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace nsaygqv0ixdkwb
{
    public sealed partial class SettingsPage : Page
    {
        private XboxGameBarWidget _widget;
        private bool _initializing;
        private bool _awaitingKeyBind;

        private static readonly double[] MagLevels = new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 8.0 };

        public SettingsPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            _widget = e.Parameter as XboxGameBarWidget;
            try
            {
                ConfigureSliderRanges();
                LoadFromSettings();
            }
            catch { }

            var coreWindow = Window.Current?.CoreWindow;
            if (coreWindow != null)
            {
                coreWindow.KeyDown += CoreWindow_KeyDown;
            }
        }

        private void ConfigureSliderRanges()
        {
            FrameSkipSlider.Maximum = 10;
            FrameSkipSlider.Minimum = 0;
            FrameSkipSlider.StepFrequency = 1;

            WidgetSizeSlider.Maximum = 1000;
            WidgetSizeSlider.Minimum = 200;
            WidgetSizeSlider.StepFrequency = 20;

            CrosshairSizeSlider.Maximum = 200;
            CrosshairSizeSlider.Minimum = 4;
            CrosshairSizeSlider.StepFrequency = 2;

            CrosshairThicknessSlider.Maximum = 10;
            CrosshairThicknessSlider.Minimum = 1;
            CrosshairThicknessSlider.StepFrequency = 1;

            CrosshairGapSlider.Maximum = 50;
            CrosshairGapSlider.Minimum = 0;
            CrosshairGapSlider.StepFrequency = 1;

            // Offsets: -halfScreen..+halfScreen. We don't know the exact monitor size
            // from the settings widget, so pick a generous range that covers 4K.
            OffsetXSlider.Maximum = 1920;
            OffsetXSlider.Minimum = -1920;
            OffsetXSlider.StepFrequency = 10;

            OffsetYSlider.Maximum = 1080;
            OffsetYSlider.Minimum = -1080;
            OffsetYSlider.StepFrequency = 10;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            var coreWindow = Window.Current?.CoreWindow;
            if (coreWindow != null)
            {
                coreWindow.KeyDown -= CoreWindow_KeyDown;
            }
        }

        private void LoadFromSettings()
        {
            _initializing = true;
            try
            {
                var s = ZoomSettings.Instance;

                EnabledSwitch.IsOn = s.Enabled;
                MagCombo.SelectedIndex = NearestMagIndex(s.Magnification);
                CircularSwitch.IsOn = s.Circular;
                LockAreaSwitch.IsOn = s.LockZoomArea;
                FrameSkipSlider.Value = s.FrameSkip;
                HotkeySwitch.IsOn = s.HotkeyEnabled;
                HotkeyModeCombo.SelectedIndex = s.HotkeyMode;
                AdaptiveSwitch.IsOn = s.AdaptivePerformance;
                WidgetSizeSlider.Value = s.RequestedWidgetSize > 0 ? s.RequestedWidgetSize : 400;

                OffsetXSlider.Value = ClampToSliderRange(s.OffsetX, OffsetXSlider);
                OffsetYSlider.Value = ClampToSliderRange(s.OffsetY, OffsetYSlider);

                CrosshairSwitch.IsOn = s.CrosshairEnabled;
                CrosshairStyleCombo.SelectedIndex = Math.Max(0, s.CrosshairStyle - 1);
                CrosshairSizeSlider.Value = s.CrosshairSize;
                CrosshairThicknessSlider.Value = s.CrosshairThickness;
                CrosshairGapSlider.Value = s.CrosshairGap;

                UpdateFrameSkipLabel();
                UpdateHotkeyLabel();
                UpdateCrosshairLabels();
                UpdateOffsetLabels();
                ApplyAdaptiveGating();

                ReloadProfileList();
            }
            finally
            {
                _initializing = false;
            }
        }

        private static int NearestMagIndex(double mag)
        {
            int idx = 0;
            double best = double.MaxValue;
            for (int i = 0; i < MagLevels.Length; i++)
            {
                double d = Math.Abs(MagLevels[i] - mag);
                if (d < best) { best = d; idx = i; }
            }
            return idx;
        }

        private void UpdateFrameSkipLabel() => FrameSkipValueLabel.Text = $"{ZoomSettings.Instance.FrameSkip}";

        private void UpdateHotkeyLabel()
        {
            if (_awaitingKeyBind)
            {
                HotkeyBindButton.Content = "Press a key...";
                return;
            }
            HotkeyBindButton.Content = VkToFriendlyName(ZoomSettings.Instance.HotkeyVirtualKey);
        }

        private static string VkToFriendlyName(int vk)
        {
            switch (vk)
            {
                case 0x12: return "Alt";
                case 0x11: return "Ctrl";
                case 0x10: return "Shift";
                case 0x20: return "Space";
                case 0x09: return "Tab";
                case 0x0D: return "Enter";
                case 0x1B: return "Esc";
                case 0x02: return "Mouse R";
                case 0x04: return "Mouse M";
                case 0x05: return "Mouse X1";
                case 0x06: return "Mouse X2";
            }
            try
            {
                var name = ((VirtualKey)vk).ToString();
                if (!string.IsNullOrEmpty(name) && name != "None") return name;
            }
            catch { }
            return $"VK 0x{vk:X2}";
        }

        private void ApplyAdaptiveGating()
        {
            bool adaptive = AdaptiveSwitch.IsOn;
            FrameSkipSlider.IsEnabled = !adaptive;
        }

        // ---- Handlers ----

        private void EnabledSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            ZoomSettings.Instance.Enabled = EnabledSwitch.IsOn;
        }

        private void MagCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_initializing) return;
            int i = Math.Max(0, MagCombo.SelectedIndex);
            if (i < MagLevels.Length)
            {
                ZoomSettings.Instance.Magnification = MagLevels[i];
            }
        }

        private void CircularSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            ZoomSettings.Instance.Circular = CircularSwitch.IsOn;
        }

        private void LockAreaSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            ZoomSettings.Instance.LockZoomArea = LockAreaSwitch.IsOn;
        }

        private void OffsetXSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_initializing) return;
            ZoomSettings.Instance.OffsetX = e.NewValue;
            UpdateOffsetLabels();
        }

        private void OffsetYSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_initializing) return;
            ZoomSettings.Instance.OffsetY = e.NewValue;
            UpdateOffsetLabels();
        }

        private void ResetOffsetsButton_Click(object sender, RoutedEventArgs e)
        {
            ZoomSettings.Instance.OffsetX = 0;
            ZoomSettings.Instance.OffsetY = 0;
            OffsetXSlider.Value = 0;
            OffsetYSlider.Value = 0;
            UpdateOffsetLabels();
        }

        private void UpdateOffsetLabels()
        {
            OffsetXLabel.Text = $"{(int)ZoomSettings.Instance.OffsetX}";
            OffsetYLabel.Text = $"{(int)ZoomSettings.Instance.OffsetY}";
        }

        private static double ClampToSliderRange(double v, Slider s)
        {
            if (v < s.Minimum) return s.Minimum;
            if (v > s.Maximum) return s.Maximum;
            return v;
        }

        private void FrameSkipSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_initializing) return;
            ZoomSettings.Instance.FrameSkip = (int)e.NewValue;
            UpdateFrameSkipLabel();
        }

        private void HotkeySwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            ZoomSettings.Instance.HotkeyEnabled = HotkeySwitch.IsOn;
        }

        private void HotkeyModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_initializing) return;
            ZoomSettings.Instance.HotkeyMode = Math.Max(0, HotkeyModeCombo.SelectedIndex);
        }

        private void HotkeyBindButton_Click(object sender, RoutedEventArgs e)
        {
            _awaitingKeyBind = true;
            UpdateHotkeyLabel();
        }

        private void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (!_awaitingKeyBind) return;

            var vk = (int)args.VirtualKey;
            if (vk == (int)VirtualKey.Escape)
            {
                _awaitingKeyBind = false;
                UpdateHotkeyLabel();
                args.Handled = true;
                return;
            }
            if (vk <= 0 || vk > 0xFF) return;

            ZoomSettings.Instance.HotkeyVirtualKey = vk;
            _awaitingKeyBind = false;
            UpdateHotkeyLabel();
            args.Handled = true;
        }

        private void AdaptiveSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            ZoomSettings.Instance.AdaptivePerformance = AdaptiveSwitch.IsOn;
            ApplyAdaptiveGating();
        }

        private void WidgetSizeSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_initializing) return;
            var s = ZoomSettings.Instance;
            s.RequestedWidgetSize = e.NewValue;
            s.RequestedWidgetSizeSeq = s.RequestedWidgetSizeSeq + 1;
        }

        private void CentralizeButton_Click(object sender, RoutedEventArgs e)
        {
            var s = ZoomSettings.Instance;
            s.RequestedCenterSeq = s.RequestedCenterSeq + 1;
        }

        // ---- Crosshair handlers ----

        private void UpdateCrosshairLabels()
        {
            CrosshairSizeLabel.Text = $"{ZoomSettings.Instance.CrosshairSize}";
            CrosshairThicknessLabel.Text = $"{ZoomSettings.Instance.CrosshairThickness}";
            CrosshairGapLabel.Text = $"{ZoomSettings.Instance.CrosshairGap}";
        }

        private void CrosshairSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            ZoomSettings.Instance.CrosshairEnabled = CrosshairSwitch.IsOn;
        }

        private void CrosshairStyleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_initializing) return;
            // ComboBox index 0 = Dot (enum 1), index 1 = Cross (enum 2), etc.
            ZoomSettings.Instance.CrosshairStyle = CrosshairStyleCombo.SelectedIndex + 1;
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                if (uint.TryParse(btn.Tag.ToString(), out uint argb))
                {
                    ZoomSettings.Instance.CrosshairColor = argb;
                }
            }
        }

        private void CrosshairSizeSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_initializing) return;
            ZoomSettings.Instance.CrosshairSize = (int)e.NewValue;
            UpdateCrosshairLabels();
        }

        private void CrosshairThicknessSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_initializing) return;
            ZoomSettings.Instance.CrosshairThickness = (int)e.NewValue;
            UpdateCrosshairLabels();
        }

        private void CrosshairGapSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_initializing) return;
            ZoomSettings.Instance.CrosshairGap = (int)e.NewValue;
            UpdateCrosshairLabels();
        }

        // ---- Profiles ----

        private bool _reloadingProfiles;

        private void ReloadProfileList()
        {
            _reloadingProfiles = true;
            try
            {
                ProfileCombo.Items.Clear();
                foreach (var name in ZoomSettings.Instance.GetProfileNames())
                {
                    ProfileCombo.Items.Add(new ComboBoxItem { Content = name });
                }
            }
            finally
            {
                _reloadingProfiles = false;
            }
        }

        private void ProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_initializing || _reloadingProfiles) return;

            string name = (ProfileCombo.SelectedItem as ComboBoxItem)?.Content as string;
            if (string.IsNullOrEmpty(name)) return;

            // Loading a profile mutates the settings, so we reload the UI from them.
            if (ZoomSettings.Instance.LoadProfile(name))
            {
                ProfileNameBox.Text = name;
                LoadFromSettings();
            }
        }

        private void ProfileSaveButton_Click(object sender, RoutedEventArgs e)
        {
            string name = ProfileNameBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name)) return;

            ZoomSettings.Instance.SaveProfile(name);
            ReloadProfileList();

            // Select the newly saved profile in the combo.
            for (int i = 0; i < ProfileCombo.Items.Count; i++)
            {
                if ((ProfileCombo.Items[i] as ComboBoxItem)?.Content as string == name)
                {
                    _reloadingProfiles = true;
                    ProfileCombo.SelectedIndex = i;
                    _reloadingProfiles = false;
                    break;
                }
            }
        }

        private void ProfileDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            string name = (ProfileCombo.SelectedItem as ComboBoxItem)?.Content as string;
            if (string.IsNullOrEmpty(name)) return;

            ZoomSettings.Instance.DeleteProfile(name);
            ReloadProfileList();
            ProfileNameBox.Text = "";
        }
    }
}
