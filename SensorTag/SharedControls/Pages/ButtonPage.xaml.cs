using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkID=390556

namespace SensorTag.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ButtonPage : Page, IWindowVisibilityWatcher
    {
        SensorTag sensor;

        public ButtonPage()
        {
            this.InitializeComponent();

            sensor = SensorTag.SelectedSensor;
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.
        /// This parameter is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ShowMessage("Connecting...");
            try
            {
                sensor.Buttons.ButtonValueChanged += Buttons_ButtonValueChanged;
                ShowMessage("");
            }
            catch (Exception ex) {
                ShowMessage(ex.Message);
            }
        }

        private void ShowMessage(string msg)
        {
            var nowait = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
            {
                Message.Text = msg;
            }));
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            sensor.Buttons.ButtonValueChanged -= Buttons_ButtonValueChanged;            
            base.OnNavigatedFrom(e);
        }

        void Buttons_ButtonValueChanged(object sender, SensorButtonEventArgs e)
        {
            var nowait = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
            {
                Button1Overlay.Visibility = (e.LeftButtonDown) ? Visibility.Visible : Visibility.Collapsed;
                Button2Overlay.Visibility = (e.RightButtonDown) ? Visibility.Visible : Visibility.Collapsed;
            }));
        }

        private void OnGoBack(object sender, RoutedEventArgs e)
        {
            this.Frame.GoBack();
        }

        public void OnVisibilityChanged(bool visible)
        {
            if (visible)
            {
                sensor.Buttons.ButtonValueChanged += Buttons_ButtonValueChanged; 
            }
            else
            {
                sensor.Buttons.ButtonValueChanged -= Buttons_ButtonValueChanged;  
            }
        }
    }
}
