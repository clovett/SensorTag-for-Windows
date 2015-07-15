using SensorTag.Controls;
using SensorTag.Pages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace SensorTag
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        DispatcherTimer _timer;
        SensorTag sensor;
        List<TileModel> tiles = new List<TileModel>();

        public MainPage()
        {
            this.InitializeComponent();
            // get the BLE services that we can share across pages.
            sensor = SensorTag.Instance;

            Clear();

            tiles.Add(new TileModel() { Caption = "Accelerometer", Icon = new BitmapImage(new Uri("ms-appx:/Assets/Accelerometer.png")) });
            tiles.Add(new TileModel() { Caption = "Gyroscope", Icon = new BitmapImage(new Uri("ms-appx:/Assets/Gyroscope.png")) });
            tiles.Add(new TileModel() { Caption = "Magnetometer", Icon = new BitmapImage(new Uri("ms-appx:/Assets/Compass.png")) });
            tiles.Add(new TileModel() { Caption = "IR Temperature", Icon = new BitmapImage(new Uri("ms-appx:/Assets/IRTemperature.png")) });
            tiles.Add(new TileModel() { Caption = "Humidity", Icon = new BitmapImage(new Uri("ms-appx:/Assets/Humidity.png")) });
            tiles.Add(new TileModel() { Caption = "Barometer", Icon = new BitmapImage(new Uri("ms-appx:/Assets/Barometer.png")) });
            tiles.Add(new TileModel() { Caption = "Buttons", Icon = new BitmapImage(new Uri("ms-appx:/Assets/Buttons.png")) });

            // these ones we always listen to.
            sensor.ServiceError += OnServiceError;
            sensor.StatusChanged += OnStatusChanged;
            sensor.PropertyChanged += OnSensorPropertyChanged;
            sensor.ConnectionChanged += OnConnectionChanged;

            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
        }

        async void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (args.VirtualKey == Windows.System.VirtualKey.F5 && !connected && !connecting)
            {
                await this.ConnectSensors();
            }
        }

        public async Task RegisterEvents(bool register)
        {
            if (register)
            {
                await sensor.Barometer.StartReading();
                await sensor.Humidity.StartReading();
                await sensor.Magnetometer.StartReading();
                await sensor.Gyroscope.StartReading(GyroscopeAxes.XYZ);
                await sensor.Accelerometer.StartReading();
                await sensor.IRTemperature.StartReading();
                sensor.Barometer.BarometerMeasurementValueChanged -= OnBarometerMeasurementValueChanged;
                sensor.Barometer.BarometerMeasurementValueChanged += OnBarometerMeasurementValueChanged;
                sensor.Humidity.HumidityMeasurementValueChanged -= OnHumidityMeasurementValueChanged;
                sensor.Humidity.HumidityMeasurementValueChanged += OnHumidityMeasurementValueChanged;
                sensor.Magnetometer.MagnetometerMeasurementValueChanged -= OnMagnetometerMeasurementValueChanged;
                sensor.Magnetometer.MagnetometerMeasurementValueChanged += OnMagnetometerMeasurementValueChanged;
                sensor.Gyroscope.GyroscopeMeasurementValueChanged -= OnGyroscopeMeasurementValueChanged;
                sensor.Gyroscope.GyroscopeMeasurementValueChanged += OnGyroscopeMeasurementValueChanged;
                sensor.Accelerometer.AccelerometerMeasurementValueChanged -= OnAccelerometerMeasurementValueChanged;
                sensor.Accelerometer.AccelerometerMeasurementValueChanged += OnAccelerometerMeasurementValueChanged;
                sensor.IRTemperature.IRTemperatureMeasurementValueChanged -= OnIRTemperatureMeasurementValueChanged;
                sensor.IRTemperature.IRTemperatureMeasurementValueChanged += OnIRTemperatureMeasurementValueChanged;
                sensor.Buttons.ButtonValueChanged -= OnButtonValueChanged;
                sensor.Buttons.ButtonValueChanged += OnButtonValueChanged;
            }
            else
            {
                await sensor.Barometer.StopReading();
                await sensor.Humidity.StopReading();
                await sensor.Magnetometer.StopReading();
                await sensor.Gyroscope.StopReading();
                await sensor.Accelerometer.StopReading();
                await sensor.IRTemperature.StopReading();
                sensor.Barometer.BarometerMeasurementValueChanged -= OnBarometerMeasurementValueChanged;
                sensor.Humidity.HumidityMeasurementValueChanged -= OnHumidityMeasurementValueChanged;
                sensor.Magnetometer.MagnetometerMeasurementValueChanged -= OnMagnetometerMeasurementValueChanged;
                sensor.Gyroscope.GyroscopeMeasurementValueChanged -= OnGyroscopeMeasurementValueChanged;
                sensor.Accelerometer.AccelerometerMeasurementValueChanged -= OnAccelerometerMeasurementValueChanged;
                sensor.IRTemperature.IRTemperatureMeasurementValueChanged -= OnIRTemperatureMeasurementValueChanged;
                sensor.Buttons.ButtonValueChanged -= OnButtonValueChanged;
            }
        }

        void OnSensorPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var nowait = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(() =>
            {
                if (e.PropertyName == "DeviceName")
                {
                    DeviceName.Text = sensor.DeviceName;
                }
            }));
        }

        void OnButtonValueChanged(object sender, SensorButtonEventArgs e)
        {
            string caption = "";
            if (e.LeftButtonDown)
            {
                caption += "Left ";
            }
            if (e.RightButtonDown)
            {
                caption += "Right";
            }
            var nowait = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(() =>
            {
                GetTile("Buttons").SensorValue = caption;
            }));
        }

        void OnStatusChanged(object sender, string status)
        {
            DisplayMessage(status);
        }

        double Fahrenheit(double celcius)
        {
            return celcius * 1.8 + 32.0;
        }


        string FormatTemperature(double t)
        {
            if (!Settings.Instance.Celcius)
            {
                t = Fahrenheit(t);
            }
            return t.ToString("N2");
        }


        void OnIRTemperatureMeasurementValueChanged(object sender, IRTemperatureMeasurementEventArgs e)
        {
            var nowait = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
            {
                // bugbug: my specific sensor seems low by about 5 degrees...
                // really need a user calibration step...
                const double fudge = 5;
                double temp = e.Measurement.ObjectTemperature + fudge;

                string suffix = Settings.Instance.Celcius ? " °C" : " °F";
                string caption = FormatTemperature(temp) + suffix;

                GetTile("IR Temperature").SensorValue = caption;
                connected = true;
            }));
        }

        private void OnAccelerometerMeasurementValueChanged(object sender, AccelerometerMeasurementEventArgs e)
        {
            var nowait = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
            {
                var m = e.Measurement;
                string caption = Math.Round(m.X, 3) + "," + Math.Round(m.Y, 3) + "," + Math.Round(m.Z, 3); 
                GetTile("Accelerometer").SensorValue = caption;
                connected = true;
            }));
        }

        private TileModel GetTile(string name)
        {
            return (from t in tiles where t.Caption == name select t).FirstOrDefault();
        }

        void OnGyroscopeMeasurementValueChanged(object sender, GyroscopeMeasurementEventArgs e)
        {
            var nowait = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
            {
                var m = e.Measurement;

                string caption = Math.Round(m.X, 3) + "," + Math.Round(m.Y, 3) + "," + Math.Round(m.Z, 3);

                GetTile("Gyroscope").SensorValue = caption;
                connected = true;
            }));
        }

        void OnMagnetometerMeasurementValueChanged(object sender, MagnetometerMeasurementEventArgs e)
        {
            var nowait = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
            {
                try
                {
                    var m = e.Measurement;

                    string caption = Math.Round(m.X, 3) + "," + Math.Round(m.Y, 3) + "," + Math.Round(m.Z, 3);

                    GetTile("Magnetometer").SensorValue = caption;
                    connected = true;
                }
                catch
                {
                }
            }));
        }

        private void OnHumidityMeasurementValueChanged(object sender, HumidityMeasurementEventArgs e)
        {
            var nowait = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
            {
                var m = e.Measurement;

                string caption = Math.Round(m.Humidity, 3) + " %rH";

                GetTile("Humidity").SensorValue = caption;
                connected = true;
            }));
        }

        static string[] pressureSuffixes = new string[] { "hPa", "Pa", "bar", "mbar", "kPa", "Hg(mm)", "Hg(in)", "psi" };


        void OnBarometerMeasurementValueChanged(object sender, BarometerMeasurementEventArgs e)
        {
            var nowait = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
            {
                var m = e.Measurement;

                var unit = Settings.Instance.PressureUnit;

                string caption = Math.Round(m.GetUnit(unit), 3) + " " + pressureSuffixes[(int)unit];

                GetTile("Barometer").SensorValue = caption;
                connected = true;
            }));
        }

        void OnServiceError(object sender, string message)
        {
            DisplayMessage(message);
        }

        private void Clear()
        {
            foreach (var tile in tiles)
            {
                tile.SensorValue = "";
            }
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.  The Parameter
        /// property is typically used to configure the page.</param>
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (!active)
            {
                active = true;
                await ConnectSensors();
            }
        }

        bool connecting;

        private async Task ConnectSensors()
        {
            try
            {
                HideHelp();
                connecting = true;
                if (await sensor.Reconnect())
                {
                    await RegisterEvents(true);
                    await sensor.Accelerometer.SetPeriod(1000); // save battery
                    SensorList.ItemsSource = tiles;
                }
                else
                {
                    ShowHelp();
                }

            } 
            catch (Exception ex)
            {
                DisplayMessage(ex.Message);
            }
            connecting = false;
        }

        private void ShowHelp()
        {
            Help.Visibility = Windows.UI.Xaml.Visibility.Visible;
        }

        private void HideHelp()
        {
            Help.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
        }

        private void StartTimer()
        {
            if (_timer == null)
            {
                _timer = new DispatcherTimer();
                _timer.Interval = TimeSpan.FromSeconds(10);
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

            }
            catch (Exception)
            {
            }
        }

        private void DisplayMessage(string message)
        {
            var nowait = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
            {
                ErrorMessage.Text = message;                
            }));
        }


        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            active = false;
            //RegisterEvents(false);
            base.OnNavigatedFrom(e);
        }

        bool active;
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

        public async void OnVisibilityChanged(bool visible)
        {
            if (visible)
            {
                if (!active)
                {
                    active = true;
                    await ConnectSensors();
                }
            }
            else
            {
                // we are leaving the app, so disconnect the bluetooth services so other apps can use them.
                active = false;
                sensor.Disconnect();
            }
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            TileModel tile = e.ClickedItem as TileModel;
            if (tile != null)
            {
                SelectTile(tile);
            }
        }

        private void SelectTile(TileModel model)
        {
            switch (model.Caption)
            {
                case "Barometer":
                    this.Frame.Navigate(typeof(PressurePage));
                    break;
                case "Accelerometer":
                    this.Frame.Navigate(typeof(AccelerometerPage));
                    break;
                case "Gyroscope":
                    this.Frame.Navigate(typeof(GyroPage));
                    break;
                case "Humidity":
                    this.Frame.Navigate(typeof(HumidityPage));
                    break;
                case "IR Temperature":
                    this.Frame.Navigate(typeof(TemperaturePage));
                    break;
                case "Magnetometer":
                    this.Frame.Navigate(typeof(MagnetometerPage));
                    break;
                case "Buttons":
                    this.Frame.Navigate(typeof(ButtonPage));
                    break;
            }
        }

        private async void OnRefresh(object sender, RoutedEventArgs e)
        {
            if(!connected && !connecting)
            {
                await this.ConnectSensors();
            }
        }
    }    
}
