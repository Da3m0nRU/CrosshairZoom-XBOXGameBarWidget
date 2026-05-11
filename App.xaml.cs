using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Microsoft.Gaming.XboxGameBar;

namespace nsaygqv0ixdkwb
{
    sealed partial class App : Application
    {
        // IDs must match Package.appxmanifest
        private const string ZoomWidgetId = "zoomwidget";
        private const string SettingsWidgetId = "zoomsettings";

        private XboxGameBarWidget zoomWidget = null;
        private XboxGameBarWidget settingsWidget = null;

        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            // Launched outside Game Bar: show a small note page via MainPage (it detects no-widget mode).
            Frame rootFrame = Window.Current.Content as Frame;
            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                Window.Current.Content = rootFrame;
            }

            if (!e.PrelaunchActivated)
            {
                if (rootFrame.Content == null)
                {
                    rootFrame.Navigate(typeof(MainPage), null);
                }
                Window.Current.Activate();
            }
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            XboxGameBarWidgetActivatedEventArgs widgetArgs = null;
            if (args.Kind == ActivationKind.Protocol)
            {
                var protocolArgs = args as IProtocolActivatedEventArgs;
                if (protocolArgs != null && protocolArgs.Uri.Scheme.Equals("ms-gamebarwidget"))
                {
                    widgetArgs = args as XboxGameBarWidgetActivatedEventArgs;
                }
            }

            if (widgetArgs == null || !widgetArgs.IsLaunchActivation)
            {
                return;
            }

            var rootFrame = new Frame();
            Window.Current.Content = rootFrame;

            string extensionId = widgetArgs.AppExtensionId;

            if (extensionId == ZoomWidgetId)
            {
                try
                {
                    zoomWidget = new XboxGameBarWidget(widgetArgs, Window.Current.CoreWindow, rootFrame);
                    rootFrame.Navigate(typeof(MainPage), zoomWidget);
                    Window.Current.Closed += (s, e2) => { zoomWidget = null; };
                }
                catch (Exception ex)
                {
                    try
                    {
                        var real = ex;
                        while (real.InnerException != null) real = real.InnerException;
                        string text =
                            ex.GetType().Name + ": " + ex.Message +
                            "\n\nInner: " + real.GetType().Name + ": " + real.Message +
                            "\n\n" + real.StackTrace;
                        rootFrame.Navigate(typeof(ErrorPage), text);
                    }
                    catch { }
                }
            }
            else if (extensionId == SettingsWidgetId)
            {
                try
                {
                    settingsWidget = new XboxGameBarWidget(widgetArgs, Window.Current.CoreWindow, rootFrame);
                    rootFrame.Navigate(typeof(SettingsPage), settingsWidget);
                    Window.Current.Closed += (s, e2) => { settingsWidget = null; };
                }
                catch (Exception ex)
                {
                    // Surface the error inside the widget instead of letting Game Bar show
                    // its generic "Something went wrong" screen. Unwrap TargetInvocationException
                    // to expose the real XAML parse error.
                    try
                    {
                        var real = ex;
                        while (real.InnerException != null) real = real.InnerException;
                        string text =
                            ex.GetType().Name + ": " + ex.Message +
                            "\n\nInner: " + real.GetType().Name + ": " + real.Message +
                            "\n\n" + real.StackTrace;
                        rootFrame.Navigate(typeof(ErrorPage), text);
                    }
                    catch { }
                }
            }
            else
            {
                return;
            }

            Window.Current.Activate();
        }

        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
    }
}
