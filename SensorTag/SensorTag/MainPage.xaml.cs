using Microsoft.MobileLabs.Bluetooth;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace SensorTag
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        const double MaxBattery = 100; // values range from 0 to 100 - it is a percentage.
        BleIRTemperatureService _tempService;
        BleAccelerometerService _accelService;
        BleGyroscopeService _gyroService;
        BleMagnetometerService _magService;
        BleHumidityService _humidityService;
        BleBarometerService _barometerService;
        DispatcherTimer _timer;

        public MainPage()
        {
            this.InitializeComponent();
            IRTemp.Text = "";
            Clear();
        }

        private void Clear()
        {
            Accelerometer.Text = "";
            Gyroscope.Text = "";
            Magnetometer.Text = "";
            Humidity.Text = "";
            BarometricPressure.Text = "";
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.  The Parameter
        /// property is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Window.Current.VisibilityChanged -= OnWindowVisibilityChanged;
            Window.Current.VisibilityChanged += OnWindowVisibilityChanged;
            Reconnect();
        }

        void OnWindowVisibilityChanged(object sender, Windows.UI.Core.VisibilityChangedEventArgs e)
        {
            Window window = (Window)sender;
            Frame frame = window.Content as Frame;
            if (frame != null)
            {
                MainPage main = frame.Content as MainPage;
                if (main != null)
                {
                    if (e.Visible)
                    {
                        Reconnect();
                    }
                    else
                    {
                        // we are leaving the app, so disconnect the bluetooth services so other apps can use them.
                        Disconnect();
                    }
                }
            }
        }


        private void StartTimer()
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(10);
            _timer.Tick += OnTimerTick;
            _timer.Start();
        }

        private async Task ConnectIRTemperatureService()
        {
            if (_tempService == null)
            {
                _tempService = new BleIRTemperatureService();
                _tempService.Error += OnServiceError;

                if (await _tempService.ConnectAsync())
                {
                    DeviceName.Text = "" + _tempService.DeviceName;
                    DisplayMessage("connected");

                    _tempService.IRTemperatureMeasurementValueChanged += OnIRTemperatureMeasurementValueChanged;
                    _tempService.ConnectionChanged += OnConnectionChanged;
                    _tempService.StartReading();
                }
            }
        }

        private async Task ConnectAccelerometerService()
        {
            if (_accelService == null)
            {
                _accelService = new BleAccelerometerService();
                _accelService.Error += OnServiceError;

                if (await _accelService.ConnectAsync())
                {
                    DeviceName.Text = "" + _accelService.DeviceName;
                    DisplayMessage("connected");

                    _accelService.AccelerometerMeasurementValueChanged += OnAccelerometerMeasurementValueChanged;
                    _accelService.ConnectionChanged += OnConnectionChanged;
                    _accelService.StartReading();
                    _accelService.SetPeriod(100); // 10 times a second.

                    int interval = await _accelService.GetPeriod();
                    Debug.WriteLine("Reading acceleration every " + interval + "ms");
                }
            }
        }

        private async Task ConnectGyroscopeService()
        {
            if (_gyroService == null)
            {
                _gyroService = new BleGyroscopeService();
                _gyroService.Error += OnServiceError;

                if (await _gyroService.ConnectAsync())
                {
                    DeviceName.Text = "" + _gyroService.DeviceName;
                    DisplayMessage("connected");

                    _gyroService.GyroscopeMeasurementValueChanged += OnGyroscopeMeasurementValueChanged;
                    _gyroService.ConnectionChanged += OnConnectionChanged;
                    _gyroService.StartReading(GyroscopeAxes.XYZ);

                }
            }
        }


        private async Task ConnectMagnetometerService()
        {
            if (_magService == null)
            {
                _magService = new BleMagnetometerService();
                _magService.Error += OnServiceError;

                if (await _magService.ConnectAsync())
                {
                    DeviceName.Text = "" + _magService.DeviceName;
                    DisplayMessage("connected");

                    _magService.MagnetometerMeasurementValueChanged += OnMagnetometerMeasurementValueChanged;
                    _magService.ConnectionChanged += OnConnectionChanged;
                    _magService.StartReading();

                    int interval = await _magService.GetPeriod();
                    Debug.WriteLine("Reading magnetometer every " + interval + "ms");
                }
            }
        }

        private async Task ConnectHumidityService()
        {
            if (_humidityService == null)
            {
                _humidityService = new BleHumidityService();
                _humidityService.Error += OnServiceError;

                if (await _humidityService.ConnectAsync())
                {
                    DeviceName.Text = "" + _humidityService.DeviceName;
                    DisplayMessage("connected");

                    _humidityService.HumidityMeasurementValueChanged += OnHumidityMeasurementValueChanged;
                    _humidityService.ConnectionChanged += OnConnectionChanged;
                    _humidityService.StartReading();
                }
            }
        }


        private async Task ConnectBarometerService()
        {
            if (_barometerService == null)
            {
                _barometerService = new BleBarometerService();
                _barometerService.Error += OnServiceError;

                if (await _barometerService.ConnectAsync())
                {
                    DeviceName.Text = "" + _barometerService.DeviceName;
                    DisplayMessage("connected");

                    BarometricPressure.Text = "calibrating...";
                    _barometerService.ConnectionChanged += OnConnectionChanged;
                    await _barometerService.StartCalibration();
                    _barometerService.BarometerMeasurementValueChanged += OnBarometerMeasurementValueChanged;
                }
            }
        }

        private double Fahrenheit(double celcius)
        {
            return 32.0 + (celcius * 9) / 5;
        }

        private void OnIRTemperatureMeasurementValueChanged(object sender, IRTemperatureMeasurementEventArgs e)
        {
            var nowait = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
            {
                // bugbug: my specific sensor seems low by about 5 degrees...
                // really need a user calibration step...
                const double fudge = 5;
                double temp = e.Measurement.ObjectTemperature + fudge;

                IRTemp.Text = Math.Round(temp, 3) + " C,  " + Math.Round(Fahrenheit(temp), 3) + "F";
                connected = true;
            }));
        }

        private void OnAccelerometerMeasurementValueChanged(object sender, AccelerometerMeasurementEventArgs e)
        {
            var nowait = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
            {
                var m = e.Measurement;

                Accelerometer.Text = Math.Round(m.X, 3) + "," + Math.Round(m.Y, 3) + "," + Math.Round(m.Z, 3);
                connected = true;
            }));
        }

        private void OnGyroscopeMeasurementValueChanged(object sender, GyroscopeMeasurementEventArgs e)
        {
            var nowait = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
            {
                var m = e.Measurement;

                Gyroscope.Text = Math.Round(m.X, 3) + "," + Math.Round(m.Y, 3) + "," + Math.Round(m.Z, 3);
                connected = true;
            }));
        }

        private void OnMagnetometerMeasurementValueChanged(object sender, MagnetometerMeasurementEventArgs e)
        {
            var nowait = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
            {
                var m = e.Measurement;

                Magnetometer.Text = Math.Round(m.X, 3) + "," + Math.Round(m.Y, 3) + "," + Math.Round(m.Z, 3);
                connected = true;
            }));
        }

        private void OnHumidityMeasurementValueChanged(object sender, HumidityMeasurementEventArgs e)
        {
            var nowait = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
            {
                var m = e.Measurement;

                Humidity.Text = Math.Round(m.Humidity, 3) + " %rH, " + Math.Round(m.Temperature, 3) + " °C";
                connected = true;
            }));
        }
        private void OnBarometerMeasurementValueChanged(object sender, BarometerMeasurementEventArgs e)
        {
            var nowait = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
            {
                var m = e.Measurement;

                BarometricPressure.Text = Math.Round(m.HectoPascals, 3) + " hPa";
                connected = true;
            }));
        }

        void OnTimerTick(object sender, object e)
        {
            try
            {

            }
            catch (Exception)
            {
            }
        }

        private void OnServiceError(object sender, string message)
        {
            DisplayMessage(message);
        }

        private void DisplayMessage(string message)
        {
            var nowait = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
            {
                ErrorMessage.Text = message;
            }));
        }

        bool connected;

        void OnConnectionChanged(object sender, ConnectionChangedEventArgs e)
        {
            if (e.IsConnected != connected)
            {
                string message = null;
                if (e.IsConnected)
                {
                    message = "connected";
                }
                else if (connected)
                {
                    message = "lost connection";
                }

                if (!e.IsConnected)
                {
                    var nowait = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
                    {
                        Clear();
                    }));
                }

                DisplayMessage(message);
            }
            connected = e.IsConnected;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            Disconnect();
            base.OnNavigatedFrom(e);
        }

        internal void Disconnect()
        {
            if (_tempService != null)
            {
                using (_tempService)
                {
                    _tempService.Error -= OnServiceError;
                    _tempService.IRTemperatureMeasurementValueChanged -= OnIRTemperatureMeasurementValueChanged;
                    _tempService.ConnectionChanged -= OnConnectionChanged;
                    _tempService = null;
                }
            }
            if (_accelService != null)
            {
                {
                    _accelService.Error -= OnServiceError;
                    _accelService.AccelerometerMeasurementValueChanged -= OnAccelerometerMeasurementValueChanged;
                    _accelService.ConnectionChanged -= OnConnectionChanged;
                    _accelService = null;
                }
            }

            if (_gyroService != null)
            {
                using (_gyroService)
                {
                    _gyroService.Error -= OnServiceError;
                    _gyroService.GyroscopeMeasurementValueChanged -= OnGyroscopeMeasurementValueChanged;
                    _gyroService.ConnectionChanged -= OnConnectionChanged;
                    _gyroService = null;
                }
            }
            if (_magService != null)
            {
                using (_magService)
                {
                    _magService.Error -= OnServiceError;
                    _magService.MagnetometerMeasurementValueChanged -= OnMagnetometerMeasurementValueChanged;
                    _magService.ConnectionChanged -= OnConnectionChanged;
                    _magService = null;
                }
            }

            if (_humidityService != null)
            {
                using (_humidityService)
                {
                    _humidityService.Error -= OnServiceError;
                    _humidityService.HumidityMeasurementValueChanged -= OnHumidityMeasurementValueChanged;
                    _humidityService.ConnectionChanged -= OnConnectionChanged;
                    _humidityService = null;
                }
            }

            if (_barometerService != null)
            {
                using (_barometerService)
                {
                    _barometerService.Error -= OnServiceError;
                    _barometerService.BarometerMeasurementValueChanged -= OnBarometerMeasurementValueChanged;
                    _barometerService.ConnectionChanged -= OnConnectionChanged;
                    _barometerService = null;
                }
            }
            if (_timer != null)
            {
                _timer.Tick -= OnTimerTick;
                _timer.Stop();
            }

        }

        bool connecting;

        internal async void Reconnect()
        {
            if (!connecting)
            {
                try
                {
                    connecting = true;
                    DisplayMessage("connecting...");
                    await ConnectIRTemperatureService();
                    await ConnectAccelerometerService();
                    await ConnectGyroscopeService();
                    await ConnectMagnetometerService();
                    await ConnectHumidityService();
                    await ConnectBarometerService();
                    StartTimer();
                }
                catch (Exception ex)
                {

                    DisplayMessage(ex.Message);
                }
                finally
                {
                    connecting = false;
                }

            }
        }
    }
}
