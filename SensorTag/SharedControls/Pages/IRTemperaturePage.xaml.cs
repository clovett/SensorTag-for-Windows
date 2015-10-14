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
    public sealed partial class TemperaturePage : Page, IWindowVisibilityWatcher
    {
        SensorTag sensor;
        DispatcherTimer _timer;
        bool celcius;

        public TemperaturePage()
        {
            this.InitializeComponent();

            sensor = SensorTag.SelectedSensor;
            celcius = Settings.Instance.Celcius;
            CelciusButton.IsChecked = celcius;
            FahrenheitButton.IsChecked = !celcius;
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
                sensor.IRTemperature.IRTemperatureMeasurementValueChanged += IRTemperature_IRTemperatureMeasurementValueChanged;
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
            sensor.IRTemperature.IRTemperatureMeasurementValueChanged -= IRTemperature_IRTemperatureMeasurementValueChanged;
            //var nowait = sensor.Barometer.StopReading();
            base.OnNavigatedFrom(e);
        }

        IRTemperatureMeasurement measurement;

        void IRTemperature_IRTemperatureMeasurementValueChanged(object sender, IRTemperatureMeasurementEventArgs e)
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
                sensor.IRTemperature.IRTemperatureMeasurementValueChanged += IRTemperature_IRTemperatureMeasurementValueChanged;
                var nowait = sensor.Humidity.StartReading();
            }
            else
            {
                sensor.IRTemperature.IRTemperatureMeasurementValueChanged -= IRTemperature_IRTemperatureMeasurementValueChanged;
                var nowait = sensor.Humidity.StopReading();
            }
        }
        private void StartTimer()
        {
            if (_timer == null)
            {
                _timer = new DispatcherTimer();
                _timer.Interval = TimeSpan.FromMilliseconds(100);
                _timer.Tick += OnTimerTick;
                _timer.Start();
            }
        }

        double Fahrenheit(double celcius)
        {
            return celcius * 1.8 + 32.0;
        }


        string FormatTemperature(double t)
        {
            if (!celcius)
            {
                t = Fahrenheit(t);
            }
            return t.ToString("N2");
        }

        bool animating;

        private void OnTimerTick(object sender, object e)
        {
            if (measurement != null)
            {
                IRTempText.Text = FormatTemperature(measurement.ObjectTemperature);
                DieTempText.Text = FormatTemperature(measurement.DieTemperature);

                if (!animating)
                {
                    animating = true;
                    IRTempGraph.Start();
                    TemperatureGraph.Start();
                }
                IRTempGraph.SetCurrentValue(measurement.ObjectTemperature);
                TemperatureGraph.SetCurrentValue(measurement.DieTemperature);
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

        private async void OnCelciusClick(object sender, RoutedEventArgs e)
        {
            celcius = true;
            FahrenheitButton.IsChecked = false;
            Settings.Instance.Celcius = true;
            await Settings.Instance.SaveAsync();            
        }

        private async void OnFahrenheitClick(object sender, RoutedEventArgs e)
        {
            celcius = false;
            CelciusButton.IsChecked = false;
            Settings.Instance.Celcius = false;
            await Settings.Instance.SaveAsync();
        }
    }
}
