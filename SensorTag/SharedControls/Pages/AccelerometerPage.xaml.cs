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
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkID=390556

namespace SensorTag.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AccelerometerPage : Page, IWindowVisibilityWatcher
    {
        SensorTag sensor;
        DispatcherTimer _timer;
        int? period;

        public AccelerometerPage()
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
                if (sensor.Version == 1)
                {
                    await sensor.Accelerometer.StartReading();
                    sensor.Accelerometer.AccelerometerMeasurementValueChanged += OnAccelerometerMeasurementValueChanged;
                    period = await sensor.Accelerometer.GetPeriod();
                }
                else if (sensor.Version == 2)
                {
                    await sensor.Movement.StartReading(MovementFlags.Accel2G | MovementFlags.AccelX | MovementFlags.AccelY | MovementFlags.AccelZ);
                    sensor.Movement.MovementMeasurementValueChanged += OnMovementMeasurementValueChanged;
                    period = await sensor.Movement.GetPeriod();
                }
                SetSensitivity(period.Value);
                ShowMessage("");
            }
            catch (Exception ex)
            {
                ShowMessage(ex.Message);
            }
        }


        double GetSensitivity()
        {
            // Slider goes from 100 to 2550 for the "period" where 2550 is the slowest and therefore the least sensitive.
            // Therefore we need to reverse the slider values in order for the slider to control sensitivity.
            return SensitivitySlider.Minimum + SensitivitySlider.Maximum - SensitivitySlider.Value;
        }

        void SetSensitivity(double value)
        {
            // Slider is reversed.  See GetSensitivity.
            SensitivitySlider.Value = SensitivitySlider.Minimum + SensitivitySlider.Maximum - value;
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
            StopTimer();
            base.OnNavigatedFrom(e);
            if (sensor.Accelerometer != null)
            {
                sensor.Accelerometer.AccelerometerMeasurementValueChanged -= OnAccelerometerMeasurementValueChanged;
            }
            else if (sensor.Movement != null)
            {
                sensor.Movement.MovementMeasurementValueChanged -= OnMovementMeasurementValueChanged;
            }
            //await sensor.Accelerometer.StopReading();
        }

        AccelerometerMeasurement measurement;
        MovementMeasurement movement;

        private void OnAccelerometerMeasurementValueChanged(object sender, AccelerometerMeasurementEventArgs e)
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

        void OnMovementMeasurementValueChanged(object sender, MovementEventArgs e)
        {
            movement = e.Measurement;
            if (_timer == null)
            {
                var nowait = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
                {
                    StartTimer();
                }));
            }
        }

        private void OnGoBack(object sender, RoutedEventArgs e)
        {
            this.Frame.GoBack();
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

        private void StopTimer()
        {
            if (_timer != null)
            {
                _timer.Tick -= OnTimerTick;
                _timer.Stop();
                _timer = null;
            }
        }

        bool animating;
        bool updatingPeriod;

        void OnTimerTick(object sender, object e)
        {
            try
            {
                if (!animating)
                {
                    animating = true;
                    XAxis.Start();
                    YAxis.Start();
                    ZAxis.Start();
                }

                double x = 0;
                double y = 0;
                double z = 0;

                if (measurement != null)
                {
                    x = measurement.X;
                    y = measurement.Y;
                    z = measurement.Z;
                }
                else if (movement != null)
                {
                    x = movement.AccelX;
                    y = movement.AccelY;
                    z = movement.AccelZ;
                }


                XAxis.SetCurrentValue(x);
                YAxis.SetCurrentValue(y);
                ZAxis.SetCurrentValue(z);

                int s = (int)Math.Max(1, GetSensitivity());

                if (period.HasValue && s != period && !updatingPeriod)
                {
                    updatingPeriod = true;
                    period = s;
                    Task.Run(new Action(UpdatePeriod));
                }

            }
            catch (Exception)
            {
            }
        }

        async void UpdatePeriod()
        {
            try
            {
                Debug.WriteLine("Period=" + period.Value);
                if (sensor.Accelerometer != null)
                {
                    await sensor.Accelerometer.SetPeriod(period.Value);
                }
                else if (sensor.Movement != null)
                {
                    await sensor.Movement.SetPeriod(period.Value);
                }
            }
            catch { }
            updatingPeriod = false;
        }


        public async void OnVisibilityChanged(bool visible)
        {
            if (visible)
            {
                if (sensor.Version == 1)
                {
                    sensor.Accelerometer.AccelerometerMeasurementValueChanged += OnAccelerometerMeasurementValueChanged;
                    await sensor.Accelerometer.StartReading();
                }
                else if (sensor.Version == 2)
                {
                    sensor.Movement.MovementMeasurementValueChanged += OnMovementMeasurementValueChanged;
                    await sensor.Movement.StartReading(MovementFlags.Accel2G | MovementFlags.AccelX | MovementFlags.AccelY | MovementFlags.AccelZ);
                }
            }
            else
            {
                if (sensor.Version == 1)
                {
                    sensor.Accelerometer.AccelerometerMeasurementValueChanged -= OnAccelerometerMeasurementValueChanged;
                    await sensor.Accelerometer.StopReading();
                }
                else if (sensor.Version == 2)
                {
                    sensor.Movement.MovementMeasurementValueChanged -= OnMovementMeasurementValueChanged;
                    await sensor.Movement.StopReading();
                }
            }
        }
    }
}
