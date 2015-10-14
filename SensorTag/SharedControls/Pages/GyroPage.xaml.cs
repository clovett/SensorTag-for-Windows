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
        DispatcherTimer timer;
        bool animating;

        public GyroPage()
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
                // gives us rotational movement every second
                if (sensor.Gyroscope != null)
                {
                    await sensor.Gyroscope.StartReading(GyroscopeAxes.XYZ);
                    await sensor.Magnetometer.StartReading();
                    sensor.Gyroscope.GyroscopeMeasurementValueChanged += Gyroscope_GyroscopeMeasurementValueChanged;

                    // use the magnetometer for absolute position
                    sensor.Magnetometer.MagnetometerMeasurementValueChanged += Magnetometer_MagnetometerMeasurementValueChanged;
                    sensor.Magnetometer.SetPeriod(100); // fastest reading
                }
                else if (sensor.Movement != null)
                {
                    await sensor.Movement.StartReading(MovementFlags.Mag | MovementFlags.GyroX | MovementFlags.GyroY | MovementFlags.GyroZ);
                    sensor.Movement.MovementMeasurementValueChanged += OnMovementMeasurementValueChanged;
                    await sensor.Movement.SetPeriod(100); // fast reading
                }
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
            if (sensor.Gyroscope != null)
            {
                sensor.Gyroscope.GyroscopeMeasurementValueChanged -= Gyroscope_GyroscopeMeasurementValueChanged;
                sensor.Magnetometer.MagnetometerMeasurementValueChanged -= Magnetometer_MagnetometerMeasurementValueChanged;
                //sensor.Gyroscope.StopReading();
                sensor.Magnetometer.SetPeriod(1000); // slow reading
            }
            else if (sensor.Movement != null)
            {
                sensor.Movement.MovementMeasurementValueChanged -= OnMovementMeasurementValueChanged;
                sensor.Movement.SetPeriod(1000); // slow reading
            }

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
        GyroscopeMeasurement measurement;

        void Gyroscope_GyroscopeMeasurementValueChanged(object sender, GyroscopeMeasurementEventArgs e)
        {
            //Debug.WriteLine("{0},{1},{2}", e.Measurement.X, e.Measurement.Y, e.Measurement.Z);

            this.measurement = e.Measurement;   

            // magnetometer has quite a bit of noise, if the value is less than 5 then chances are the
            // device is not moving.
            if (Math.Abs(measurement.X) > MinimumMovement)
            {
                rx += measurement.X;
                speedX = 1d + Math.Min(3, Math.Abs(measurement.X / 10));
            }
            if (Math.Abs(measurement.Y) > MinimumMovement)
            {
                ry += measurement.Y;
                speedY = 1d + Math.Min(3, Math.Abs(measurement.Y / 10));
            }
            if (Math.Abs(measurement.Z) > MinimumMovement)
            {
                rz += measurement.Z;
                speedZ = 1d + Math.Min(3, Math.Abs(measurement.Z / 10));
            }

            if (timer == null)
            {
                var nowait = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
                {
                    StartTimer();
                }));
            }
        }

        MovementMeasurement movement;

        void OnMovementMeasurementValueChanged(object sender, MovementEventArgs e)
        {
            this.movement = e.Measurement;

            this.absolutePosition = new MagnetometerMeasurement()
            {
                X = movement.MagX,
                Y = movement.MagY,
                Z = movement.MagZ
            };

            // magnetometer has quite a bit of noise, if the value is less than 5 then chances are the
            // device is not moving.
            if (Math.Abs(movement.GyroX) > MinimumMovement)
            {
                rx += movement.MagX;
                speedX = 1d + Math.Min(3, Math.Abs(movement.MagX / 10));
            }
            if (Math.Abs(movement.GyroY) > MinimumMovement)
            {
                ry += movement.MagY;
                speedY = 1d + Math.Min(3, Math.Abs(movement.MagY / 10));
            }
            if (Math.Abs(movement.GyroZ) > MinimumMovement)
            {
                rz += movement.MagZ;
                speedZ = 1d + Math.Min(3, Math.Abs(movement.MagZ / 10));
            }

            if (timer == null)
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
            if (timer == null)
            {
                timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromMilliseconds(30);
                timer.Tick += OnTimerTick;
                timer.Start();
            }
        }

        private void StopTimer()
        {
            if (timer != null)
            {
                timer.Tick -= OnTimerTick;
                timer.Stop();
                timer = null;
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
                    x = movement.GyroX;
                    y = movement.GyroY;
                    z = movement.GyroZ;
                }
                if (!animating)
                {
                    animating = true;
                    XAxis.Start();
                    YAxis.Start();
                    ZAxis.Start();
                }
                XAxis.SetCurrentValue(ClampX(x));
                YAxis.SetCurrentValue(ClampY(y));
                ZAxis.SetCurrentValue(ClampZ(z));
            }
            catch (Exception)
            {
            }
        }

        double maxX = 0;
        double ClampX(double x)
        {
            if (Math.Abs(x) > maxX)
            {
                maxX = Math.Abs(x);
            }
            return x / maxX;
        }

        double maxY = 0;
        double ClampY(double y)
        {
            if (Math.Abs(y) > maxY)
            {
                maxY = Math.Abs(y);
            }
            return y / maxY;
        }

        double maxZ = 0;
        double ClampZ(double z)
        {
            if (Math.Abs(z) > maxZ)
            {
                maxZ = Math.Abs(z);
            }
            return z / maxZ;
        }

        public async void OnVisibilityChanged(bool visible)
        {
            if (visible)
            {
                if (sensor.Gyroscope != null)
                {
                    await sensor.Gyroscope.StartReading(GyroscopeAxes.XYZ);
                    sensor.Gyroscope.GyroscopeMeasurementValueChanged += Gyroscope_GyroscopeMeasurementValueChanged;
                }
                else if (sensor.Movement != null)
                {
                    await sensor.Movement.StartReading(MovementFlags.Mag | MovementFlags.GyroX | MovementFlags.GyroY | MovementFlags.GyroZ);
                    sensor.Movement.MovementMeasurementValueChanged += OnMovementMeasurementValueChanged;
                }
            }
            else
            {
                if (sensor.Gyroscope != null)
                {
                    sensor.Gyroscope.GyroscopeMeasurementValueChanged -= Gyroscope_GyroscopeMeasurementValueChanged;
                    await sensor.Gyroscope.StopReading();
                } 
                else if (sensor.Movement != null)
                {
                    sensor.Movement.MovementMeasurementValueChanged -= OnMovementMeasurementValueChanged;
                    await sensor.Movement.StopReading();
                }
            }
        }
    }
}
