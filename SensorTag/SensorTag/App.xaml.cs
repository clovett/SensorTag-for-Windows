using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Phone.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Application template is documented at http://go.microsoft.com/fwlink/?LinkId=234227

namespace SensorTag
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        Settings _settings;
        SensorTag _sensor;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
            HardwareButtons.BackPressed += HardwareButtons_BackPressed;

        }

        /// <summary>
        /// The selected SensorTag device
        /// </summary>
        public SensorTag SensorTag
        {
            get { return _sensor; }
            set { _sensor = value; }
        }


        /// <summary>
        /// Handles back button press.  If app is at the root page of app, don't go back and the
        /// system will suspend the app.
        /// </summary>
        /// <param name="sender">The source of the BackPressed event.</param>
        /// <param name="e">Details for the BackPressed event.</param>
        private void HardwareButtons_BackPressed(object sender, BackPressedEventArgs e)
        {
            Frame frame = Window.Current.Content as Frame;
            if (frame == null)
            {
                return;
            }

            if (frame.CanGoBack)
            {
                frame.GoBack();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used when the application is launched to open a specific file, to display
        /// search results, and so forth.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected async override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            _settings = await Settings.LoadAsync();
            if (_settings == null)
            {
                _settings = new Settings();
                await _settings.SaveAsync();
            }

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: Load state from previously suspended application
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
                // When the navigation stack isn't restored navigate to the first page,
                // configuring the new page by passing required information as a navigation
                // parameter
                if (!rootFrame.Navigate(typeof(MainPage), e.Arguments))
                {
                    throw new Exception("Failed to create initial page");
                }
            }

            // Ensure the current window is active
            Window.Current.Activate();

            // Make sure we are listening to this event
            Window.Current.VisibilityChanged -= OnWindowVisibilityChanged;
            Window.Current.VisibilityChanged += OnWindowVisibilityChanged;

        }

        private void OnWindowVisibilityChanged(object sender, Windows.UI.Core.VisibilityChangedEventArgs e)
        {
            Frame frame = Window.Current.Content as Frame;
            IWindowVisibilityWatcher watcher = frame.Content as IWindowVisibilityWatcher;
            if (watcher != null)
            {
                watcher.OnVisibilityChanged(e.Visible);
            }
            
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            Window.Current.VisibilityChanged -= OnWindowVisibilityChanged;
            deferral.Complete();
        }
    }
}
