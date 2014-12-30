using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkID=390556

namespace SensorTag.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MagnetometerPage : Page, IWindowVisibilityWatcher
    {
        SensorTag sensor;
        DispatcherTimer _timer;

        public MagnetometerPage()
        {
            this.InitializeComponent();

            sensor = SensorTag.Instance;
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
                sensor.Magnetometer.MagnetometerMeasurementValueChanged += Magnetometer_MagnetometerMeasurementValueChanged;
                sensor.Magnetometer.StartReading();
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
            sensor.Magnetometer.MagnetometerMeasurementValueChanged -= Magnetometer_MagnetometerMeasurementValueChanged;
            sensor.Barometer.StopReading();
            base.OnNavigatedFrom(e);
        }

        MagnetometerMeasurement measurement;

        void Magnetometer_MagnetometerMeasurementValueChanged(object sender, MagnetometerMeasurementEventArgs e)
        {
            measurement = e.Measurement;

            if (_timer == null)
            {
                var nowait = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
                {
                    StartTimer();
                }));
            }
        }

        string GetCaption(double value)
        {
            return Math.Round(value, 2).ToString();
        }

        private void OnGoBack(object sender, RoutedEventArgs e)
        {
            this.Frame.GoBack();
        }

        public void OnVisibilityChanged(bool visible)
        {
            if (visible)
            {
                sensor.Magnetometer.MagnetometerMeasurementValueChanged += Magnetometer_MagnetometerMeasurementValueChanged;
                sensor.Barometer.StartReading();
            }
            else
            {
                sensor.Magnetometer.MagnetometerMeasurementValueChanged -= Magnetometer_MagnetometerMeasurementValueChanged;
                sensor.Barometer.StopReading();
            }
        }
        private void StartTimer()
        {
            if (_timer == null)
            {
                _timer = new DispatcherTimer();
                _timer.Interval = TimeSpan.FromMilliseconds(30);
                _timer.Tick += OnTimerTick;
                _timer.Start();
            }
        }

        private void OnTimerTick(object sender, object e)
        {
            AnimationHelper.BeginAnimation(XCompass, new DoubleAnimation() { Duration = new Duration(TimeSpan.FromMilliseconds(100)), To = measurement.X, EnableDependentAnimation = true }, "Angle", null);
            AnimationHelper.BeginAnimation(YCompass, new DoubleAnimation() { Duration = new Duration(TimeSpan.FromMilliseconds(100)), To = measurement.Y, EnableDependentAnimation = true }, "Angle", null);
            AnimationHelper.BeginAnimation(ZCompass, new DoubleAnimation() { Duration = new Duration(TimeSpan.FromMilliseconds(100)), To = measurement.Z, EnableDependentAnimation = true }, "Angle", null);
        }

        private void StopTimer()
        {
            if (_timer != null)
            {
                _timer.Tick -= OnTimerTick;
                _timer.Stop();
                _timer = null;
            }
        }

    }
}
