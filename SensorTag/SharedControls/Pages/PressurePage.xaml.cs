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
        //int? period;
        DispatcherTimer _timer;
        PressureUnit selectedUnit;

        public PressurePage()
        {
            this.InitializeComponent();

            string[] units = new string[] { "hectopascal", "pascal", "bar", "millibar", "kilopascal", "Mercury (mm)", "Mercury (inches)", "psi" };
            UnitCombo.ItemsSource = units;
            
            selectedUnit = (PressureUnit)Settings.Instance.PressureUnit;
            // some sanity checks
            if (selectedUnit < PressureUnit.Hectopascal)
            {
                selectedUnit = PressureUnit.Hectopascal;
            }
            if (selectedUnit > PressureUnit.Psi)
            {
                selectedUnit = PressureUnit.Psi;
            }
            UnitCombo.SelectedIndex = (int)selectedUnit;

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
                await sensor.Barometer.StartReading();
                sensor.Barometer.BarometerMeasurementValueChanged += OnBarometerMeasurementValueChanged;
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
            //sensor.Barometer.StopReading();
            base.OnNavigatedFrom(e);
        }

        string caption;

        private void OnBarometerMeasurementValueChanged(object sender, BarometerMeasurementEventArgs e)
        {
            caption = GetCaption(e.Measurement.GetUnit(this.selectedUnit));

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

        //bool updatingPeriod;

        private void OnTimerTick(object sender, object e)
        {
            if (ValueText.Text != caption)
            {
                ValueText.Text = caption;
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

        private async void OnUnitChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedUnit = (PressureUnit)UnitCombo.SelectedIndex;

            // remember this setting.
            Settings.Instance.PressureUnit = (int)selectedUnit;
            await Settings.Instance.SaveAsync();
        }

    }
}
