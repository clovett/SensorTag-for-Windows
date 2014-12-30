using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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
    public sealed partial class GyroPage : Page
    {
        SensorTag sensor;
        DispatcherTimer _timer;

        public GyroPage()
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
                // gives us rotational movement every second
                sensor.Gyroscope.GyroscopeMeasurementValueChanged += Gyroscope_GyroscopeMeasurementValueChanged;
                sensor.Gyroscope.StartReading(GyroscopeAxes.XYZ);
                // use the magnetometer for absolute position
                sensor.Magnetometer.MagnetometerMeasurementValueChanged += Magnetometer_MagnetometerMeasurementValueChanged;
                sensor.Magnetometer.StartReading();
                ShowMessage("");
            }
            catch (Exception ex)
            {
                ShowMessage(ex.Message);
            }
        }

        MagnetometerMeasurement absolutePosition;

        void Magnetometer_MagnetometerMeasurementValueChanged(object sender, MagnetometerMeasurementEventArgs e)
        {
            absolutePosition = e.Measurement;
            Debug.WriteLine("{0}\t{1}\t{2}", absolutePosition.X, absolutePosition.Y, absolutePosition.Z);
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
            sensor.Gyroscope.GyroscopeMeasurementValueChanged -= Gyroscope_GyroscopeMeasurementValueChanged;
            sensor.Gyroscope.StopReading();
            StopTimer();
            base.OnNavigatedFrom(e);
        }

        double rx;
        double ry;
        double rz;
        const double MinimumMovement = 5d;
        double speedX;
        double speedY;
        double speedZ;


        void Gyroscope_GyroscopeMeasurementValueChanged(object sender, GyroscopeMeasurementEventArgs e)
        {
            var m = e.Measurement;   
            // magnetometer has quite a bit of noise, if the value is less than 5 then chances are the
            // device is not moving.
            if (Math.Abs(m.X) > MinimumMovement)
            {
                rx += m.X;
                speedX = 1d + Math.Min(3, Math.Abs(m.X / 10));
            }
            if (Math.Abs(m.Y) > MinimumMovement)
            {
                ry += m.Y;
                speedY = 1d + Math.Min(3, Math.Abs(m.Y / 10));
            }
            if (Math.Abs(m.Z) > MinimumMovement)
            {
                rz += m.Z;
                speedZ = 1d + Math.Min(3, Math.Abs(m.Z / 10));
            }

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

        void OnTimerTick(object sender, object e)
        {
            try
            {
                // gyro only gives reading every second, but we want to animate the rotation 30 times a second.
                // we also have to flip X and Y coordinates to match the pretty picture on screen.

                if (rx > MinimumMovement)
                {
                    BorderProjection.RotationY += speedX;
                    rx -= speedX;
                }
                else if (rx < -MinimumMovement)
                {
                    BorderProjection.RotationY -= speedX;
                    rx += speedX;
                }
                if (ry > MinimumMovement)
                {
                    BorderProjection.RotationX += speedY;
                    ry -= speedY;
                }
                else if (ry < -MinimumMovement)
                {
                    BorderProjection.RotationX -= speedY;
                    ry += speedY;
                }
                if (rz > MinimumMovement)
                {
                    BorderProjection.RotationZ += speedZ;
                    rz -= speedZ;
                }
                else if (rz < -MinimumMovement)
                {
                    BorderProjection.RotationZ -= speedZ;
                    rz += speedZ;
                }
            }
            catch (Exception)
            {
            }
        }

        public void OnVisibilityChanged(bool visible)
        {
            if (visible)
            {
                sensor.Gyroscope.GyroscopeMeasurementValueChanged += Gyroscope_GyroscopeMeasurementValueChanged;
                sensor.Accelerometer.StartReading();
            }
            else
            {
                sensor.Gyroscope.GyroscopeMeasurementValueChanged -= Gyroscope_GyroscopeMeasurementValueChanged;
                sensor.Accelerometer.StopReading();
            }
        }
    }
}
