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
                    await sensor.Magnetometer.StartReading();
                    sensor.Magnetometer.MagnetometerMeasurementValueChanged += Magnetometer_MagnetometerMeasurementValueChanged;
                }
                else if (sensor.Version == 2)
                {
                    await sensor.Movement.StartReading(MovementFlags.Mag);
                    sensor.Movement.MovementMeasurementValueChanged += OnMovementMeasurementValueChanged;
                }
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
            base.OnNavigatedFrom(e);
            if (sensor.Magnetometer != null)
            {
                sensor.Magnetometer.MagnetometerMeasurementValueChanged -= Magnetometer_MagnetometerMeasurementValueChanged;
            }
            else if (sensor.Movement != null)
            {
                sensor.Movement.MovementMeasurementValueChanged -= OnMovementMeasurementValueChanged;
            }
            //await sensor.Barometer.StopReading();

            StopTimer();
        }

        MagnetometerMeasurement measurement;
        MovementMeasurement movement;

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

        string GetCaption(double value)
        {
            return Math.Round(value, 2).ToString();
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
                _timer.Interval = TimeSpan.FromMilliseconds(100);
                _timer.Tick += OnTimerTick;
                _timer.Start();
            }
        }

        bool animating;
        private void OnTimerTick(object sender, object e)
        {
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
                x = movement.MagX;
                y = movement.MagY;
                z = movement.MagZ;
            }

            double xAngle = x * (360 / 30);
            AnimationHelper.BeginAnimation(XCompass, new DoubleAnimation() { Duration = new Duration(TimeSpan.FromMilliseconds(100)), To = xAngle, EnableDependentAnimation = true }, "Angle", null);

            double yAngle = y * (360 / 30);
            AnimationHelper.BeginAnimation(YCompass, new DoubleAnimation() { Duration = new Duration(TimeSpan.FromMilliseconds(100)), To = yAngle, EnableDependentAnimation = true }, "Angle", null);
            
            double zAngle = z * (360 / 100);
            AnimationHelper.BeginAnimation(ZCompass, new DoubleAnimation() { Duration = new Duration(TimeSpan.FromMilliseconds(100)), To = zAngle, EnableDependentAnimation = true }, "Angle", null);

            if (!animating)
            {
                animating = true;
                XAxis.Start();
                YAxis.Start();
                ZAxis.Start();
            }
            XAxis.SetCurrentValue(x);
            YAxis.SetCurrentValue(y);
            ZAxis.SetCurrentValue(z);

        }

        public async void OnVisibilityChanged(bool visible)
        {
            if (visible)
            {
                if (sensor.Magnetometer != null)
                {
                    await sensor.Magnetometer.StartReading();
                    sensor.Magnetometer.MagnetometerMeasurementValueChanged += Magnetometer_MagnetometerMeasurementValueChanged;
                }
                else if (sensor.Movement != null)
                {
                    await sensor.Movement.StartReading(MovementFlags.Mag);
                    sensor.Movement.MovementMeasurementValueChanged += OnMovementMeasurementValueChanged;
                }
            }
            else
            {
                if (sensor.Magnetometer != null)
                {
                    sensor.Magnetometer.MagnetometerMeasurementValueChanged -= Magnetometer_MagnetometerMeasurementValueChanged;
                    await sensor.Magnetometer.StopReading();
                }
                else if (sensor.Movement != null)
                {
                    sensor.Movement.MovementMeasurementValueChanged -= OnMovementMeasurementValueChanged;
                    await sensor.Movement.StopReading();
                }
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
