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
    public sealed partial class HumidityPage : Page, IWindowVisibilityWatcher
    {
        SensorTag sensor;
        DispatcherTimer _timer;

        public HumidityPage()
        {
            this.InitializeComponent();
            sensor = SensorTag.SelectedSensor;
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.
        /// This parameter is typically used to configure the page.</param>
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            ShowMessage("Connecting...");
            try
            {
                await sensor.Humidity.StartReading();
                sensor.Humidity.HumidityMeasurementValueChanged += Humidity_HumidityMeasurementValueChanged;
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
            sensor.Humidity.HumidityMeasurementValueChanged -= Humidity_HumidityMeasurementValueChanged;
            //sensor.Barometer.StopReading();
            base.OnNavigatedFrom(e);
        }

        HumidityMeasurement measurement;
        
        void Humidity_HumidityMeasurementValueChanged(object sender, HumidityMeasurementEventArgs e)
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
                sensor.Humidity.HumidityMeasurementValueChanged += Humidity_HumidityMeasurementValueChanged;
                sensor.Humidity.StartReading();
            }
            else
            {
                sensor.Humidity.HumidityMeasurementValueChanged -= Humidity_HumidityMeasurementValueChanged;
                sensor.Humidity.StopReading();
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

        bool animating;

        private void OnTimerTick(object sender, object e)
        {
            if (measurement != null)
            {
                HumidityText.Text = ((int)measurement.Humidity).ToString();
                TemperatureText.Text = ((int)measurement.Temperature).ToString();

                if (!animating)
                {
                    animating = true;
                    HumidityGraph.Start();
                    TemperatureGraph.Start();
                }
                HumidityGraph.SetCurrentValue(measurement.Humidity);
                TemperatureGraph.SetCurrentValue(measurement.Temperature);
            }
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
