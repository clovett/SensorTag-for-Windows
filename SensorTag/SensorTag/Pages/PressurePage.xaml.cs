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
    public sealed partial class PressurePage : Page, IWindowVisibilityWatcher
    {
        SensorTag sensor;
        int? period;
        DispatcherTimer _timer;
        string selectedUnit;

        public PressurePage()
        {
            this.InitializeComponent();

            string[] units = new string[] { "hectopascal", "pascal", "bar", "millibar", "kilopascal", "Mercury (mm)", "Mercury (inches)", "psi" };
            UnitCombo.ItemsSource = units;
            UnitCombo.SelectedIndex = 0;

            sensor = SensorTag.Instance;
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
                sensor.Barometer.BarometerMeasurementValueChanged += OnBarometerMeasurementValueChanged;
                sensor.Barometer.StartReading();
                //period = await sensor.Barometer.GetPeriod();
                //SensitivitySlider.Value = period.Value;
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
            sensor.Barometer.BarometerMeasurementValueChanged -= OnBarometerMeasurementValueChanged;
            sensor.Barometer.StopReading();
            base.OnNavigatedFrom(e);
        }

        string caption;

        private void OnBarometerMeasurementValueChanged(object sender, BarometerMeasurementEventArgs e)
        {
            switch (selectedUnit)
            {
                case "hectopascal":
                    caption = GetCaption(e.Measurement.HectoPascals);
                    break;
                case "pascal":
                    caption = GetCaption(e.Measurement.Pascals);
                    break;
                case "bar":
                    caption = GetCaption(e.Measurement.Bars);
                    break;
                case "millibar":
                    caption = GetCaption(e.Measurement.MilliBars);
                    break;
                case "kilopascal":
                    caption = GetCaption(e.Measurement.KiloPascals);
                    break;
                case "Mercury (mm)":
                    caption = GetCaption(e.Measurement.HgMm);
                    break;
                case "Mercury (inches)":
                    caption = GetCaption(e.Measurement.HgInches);
                    break;
                case "psi":
                    caption = GetCaption(e.Measurement.Psi);
                    break;
            }

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
                sensor.Barometer.BarometerMeasurementValueChanged += OnBarometerMeasurementValueChanged;
                sensor.Barometer.StartReading();
            }
            else
            {
                sensor.Barometer.BarometerMeasurementValueChanged -= OnBarometerMeasurementValueChanged;
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

        bool updatingPeriod;

        private void OnTimerTick(object sender, object e)
        {
            //if (period.HasValue && (int)SensitivitySlider.Value != period && !updatingPeriod)
            //{
            //    updatingPeriod = true;
            //    period = (int)SensitivitySlider.Value;
            //    Task.Run(new Action(UpdatePeriod));
            //}

            if (ValueText.Text != caption)
            {
                ValueText.Text = caption;
            }

        }

        //async void UpdatePeriod()
        //{
        //    try
        //    {
        //        await sensor.Barometer.SetPeriod(period.Value);
        //    }
        //    catch { }
        //    updatingPeriod = false;
        //}

        private void StopTimer()
        {
            if (_timer != null)
            {
                _timer.Tick -= OnTimerTick;
                _timer.Stop();
                _timer = null;
            }
        }

        private void OnUnitChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedUnit = (string)UnitCombo.SelectedItem;
        }

    }
}
