using SensorTag.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
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

namespace SensorTag.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class DevicePage : Page
    {
        DispatcherTimer _timer;
        SensorTag sensor;
        bool registeredConnectionEvents;
        ObservableCollection<TileModel> tiles = new ObservableCollection<TileModel>();

        public DevicePage()
        {
            this.InitializeComponent();

            Clear();

        }

        private async void OnLabelChanged(object sender, EventArgs e)
        {
            string newName = DeviceName.Label;
            Settings.Instance.SetName(sensor.DeviceAddress, newName);
            await Settings.Instance.SaveAsync();
            SetDeviceName();
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            sensor = e.Parameter as SensorTag;

            SensorList.ItemsSource = tiles;
            SetDeviceName();

            base.OnNavigatedTo(e);
            await this.ConnectSensors();
        }

        private void SetDeviceName()
        {
            string name = Settings.Instance.FindName(sensor.DeviceAddress);
            if (string.IsNullOrEmpty(name))
            {
                name = sensor.DeviceName;
            }

            DeviceName.LabelChanged -= OnLabelChanged;
            DeviceName.Label = name;
            DeviceName.LabelChanged += OnLabelChanged;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            active = false;
            if (sensor != null && e.NavigationMode == NavigationMode.Back)
            {
                sensor.Disconnect();
            }
        }

        public async Task RegisterBarometer(bool register)
        {
            try
            {
                if (register)
                {
                    await sensor.Barometer.StartReading();
                    sensor.Barometer.BarometerMeasurementValueChanged -= OnBarometerMeasurementValueChanged;
                    sensor.Barometer.BarometerMeasurementValueChanged += OnBarometerMeasurementValueChanged;
                    AddTile(new TileModel() { Caption = "Barometer", Icon = new BitmapImage(new Uri("ms-appx:/Assets/Barometer.png")) });
                }
                else
                {
                    RemoveTiles(from t in tiles where t.Caption == "Barometer" select t);
                    await sensor.Barometer.StopReading();
                    sensor.Barometer.BarometerMeasurementValueChanged -= OnBarometerMeasurementValueChanged;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("### Error registering barometer: " + ex.Message);
            }
        }

        public async Task RegisterAccelerometer(bool register)
        {
            try
            {
                if (register)
                {
                    await sensor.Accelerometer.StartReading();
                    sensor.Accelerometer.AccelerometerMeasurementValueChanged -= OnAccelerometerMeasurementValueChanged;
                    sensor.Accelerometer.AccelerometerMeasurementValueChanged += OnAccelerometerMeasurementValueChanged;
                    AddTile(new TileModel() { Caption = "Accelerometer", Icon = new BitmapImage(new Uri("ms-appx:/Assets/Accelerometer.png")) });
                }
                else
                {
                    RemoveTiles(from t in tiles where t.Caption == "Accelerometer" select t);
                    await sensor.Accelerometer.StopReading();
                    sensor.Accelerometer.AccelerometerMeasurementValueChanged -= OnAccelerometerMeasurementValueChanged;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("### Error registering Accelerometer: " + ex.Message);
            }
        }

        public async Task RegisterGyroscope(bool register)
        {
            try
            {
                if (register)
                {
                    await sensor.Gyroscope.StartReading(GyroscopeAxes.XYZ);
                    sensor.Gyroscope.GyroscopeMeasurementValueChanged -= OnGyroscopeMeasurementValueChanged;
                    sensor.Gyroscope.GyroscopeMeasurementValueChanged += OnGyroscopeMeasurementValueChanged;
                    AddTile(new TileModel() { Caption = "Gyroscope", Icon = new BitmapImage(new Uri("ms-appx:/Assets/Gyroscope.png")) });
                }
                else
                {
                    RemoveTiles(from t in tiles where t.Caption == "Gyroscope" select t);
                    await sensor.Gyroscope.StopReading();
                    sensor.Gyroscope.GyroscopeMeasurementValueChanged -= OnGyroscopeMeasurementValueChanged;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("### Error registering Gyroscope: " + ex.Message);
            }
        }

        public async Task RegisterMagnetometer(bool register)
        {
            try
            {
                if (register)
                {
                    await sensor.Magnetometer.StartReading();
                    sensor.Magnetometer.MagnetometerMeasurementValueChanged -= OnMagnetometerMeasurementValueChanged;
                    sensor.Magnetometer.MagnetometerMeasurementValueChanged += OnMagnetometerMeasurementValueChanged;
                    tiles.Add(new TileModel() { Caption = "Magnetometer", Icon = new BitmapImage(new Uri("ms-appx:/Assets/Compass.png")) });
                }
                else
                {
                    RemoveTiles(from t in tiles where t.Caption == "Magnetometer" select t);
                    await sensor.Magnetometer.StopReading();
                    sensor.Magnetometer.MagnetometerMeasurementValueChanged -= OnMagnetometerMeasurementValueChanged;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("### Error registering Magnetometer: " + ex.Message);
            }
        }

        public async Task RegisterIRTemperature(bool register)
        {
            try
            {
                if (register)
                {
                    await sensor.IRTemperature.StartReading();
                    sensor.IRTemperature.IRTemperatureMeasurementValueChanged -= OnIRTemperatureMeasurementValueChanged;
                    sensor.IRTemperature.IRTemperatureMeasurementValueChanged += OnIRTemperatureMeasurementValueChanged;
                    tiles.Add(new TileModel() { Caption = "IR Temperature", Icon = new BitmapImage(new Uri("ms-appx:/Assets/IRTemperature.png")) });
                }
                else
                {
                    RemoveTiles(from t in tiles where t.Caption == "IR Temperature" select t);
                    await sensor.IRTemperature.StopReading();
                    sensor.IRTemperature.IRTemperatureMeasurementValueChanged -= OnIRTemperatureMeasurementValueChanged;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("### Error registering IRTemperature: " + ex.Message);
            }
        }


        public async Task RegisterHumidity(bool register)
        {
            try
            {
                if (register)
                {
                    await sensor.Humidity.StartReading();
                    sensor.Humidity.HumidityMeasurementValueChanged -= OnHumidityMeasurementValueChanged;
                    sensor.Humidity.HumidityMeasurementValueChanged += OnHumidityMeasurementValueChanged;
                    tiles.Add(new TileModel() { Caption = "Humidity", Icon = new BitmapImage(new Uri("ms-appx:/Assets/Humidity.png")) });
                }
                else
                {
                    RemoveTiles(from t in tiles where t.Caption == "Humidity" select t);
                    await sensor.Humidity.StopReading();
                    sensor.Humidity.HumidityMeasurementValueChanged -= OnHumidityMeasurementValueChanged;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("### Error registering Humidity: " + ex.Message);
            }
        }

        public void RegisterButtons(bool register)
        {
            try
            {
                if (register)
                {
                    sensor.Buttons.ButtonValueChanged -= OnButtonValueChanged;
                    sensor.Buttons.ButtonValueChanged += OnButtonValueChanged;
                    tiles.Add(new TileModel() { Caption = "Buttons", Icon = new BitmapImage(new Uri("ms-appx:/Assets/Buttons.png")) });
                }
                else
                {
                    RemoveTiles(from t in tiles where t.Caption == "Humidity" select t);
                    sensor.Buttons.ButtonValueChanged -= OnButtonValueChanged;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("### Error registering Humidity: " + ex.Message);
            }
        }

        public async Task RegisterMovement(bool register)
        {
            try
            {
                if (register)
                {
                    await sensor.Movement.StartReading(MovementFlags.Accel2G | MovementFlags.AccelX | MovementFlags.AccelY | MovementFlags.AccelZ | MovementFlags.GyroX | MovementFlags.GyroY | MovementFlags.GyroZ | MovementFlags.Mag);

                    sensor.Movement.MovementMeasurementValueChanged -= OnMovementMeasurementValueChanged;
                    sensor.Movement.MovementMeasurementValueChanged += OnMovementMeasurementValueChanged;

                    // This is a combined sensor
                    AddTile(new TileModel() { Caption = "Accelerometer", Icon = new BitmapImage(new Uri("ms-appx:/Assets/Accelerometer.png")) });
                    AddTile(new TileModel() { Caption = "Gyroscope", Icon = new BitmapImage(new Uri("ms-appx:/Assets/Gyroscope.png")) });
                    tiles.Add(new TileModel() { Caption = "Magnetometer", Icon = new BitmapImage(new Uri("ms-appx:/Assets/Compass.png")) });
                }
                else
                {
                    RemoveTiles(from t in tiles where t.Caption == "Accelerometer" select t);
                    RemoveTiles(from t in tiles where t.Caption == "Gyroscope" select t);
                    RemoveTiles(from t in tiles where t.Caption == "Magnetometer" select t);
                    await sensor.Movement.StopReading();
                    sensor.Movement.MovementMeasurementValueChanged -= OnMovementMeasurementValueChanged;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("### Error registering Movement: " + ex.Message);
            }
        }



        public async Task RegisterLightIntensity(bool register)
        {
            try
            {
                if (register)
                {
                    await sensor.LightIntensity.StartReading();
                    sensor.LightIntensity.LightMeasurementValueChanged -= OnLightMeasurementValueChanged;
                    sensor.LightIntensity.LightMeasurementValueChanged += OnLightMeasurementValueChanged;
                    tiles.Add(new TileModel() { Caption = "Light", Icon = new BitmapImage(new Uri("ms-appx:/Assets/Eye.png")) });
                }
                else
                {
                    RemoveTiles(from t in tiles where t.Caption == "Light" select t);
                    await sensor.LightIntensity.StopReading();
                    sensor.LightIntensity.LightMeasurementValueChanged -= OnLightMeasurementValueChanged;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("### Error registering LightIntensity: " + ex.Message);
            }
        }

        public async Task RegisterEvents(bool register)
        {
            // these ones we always listen to.
            if (!registeredConnectionEvents)
            {
                registeredConnectionEvents = true;
                sensor.ServiceError += OnServiceError;
                sensor.StatusChanged += OnStatusChanged;
                sensor.ConnectionChanged += OnConnectionChanged;
            }

            await RegisterBarometer(register);
            await RegisterIRTemperature(register);
            await RegisterHumidity(register);

            if (sensor.Version == 1)
            {
                await RegisterAccelerometer(register);
                await RegisterGyroscope(register);
                await RegisterMagnetometer(register);
                RegisterButtons(register);
            }
            if (sensor.Version == 2)
            {
                await RegisterMovement(register);
                await RegisterLightIntensity(register);
            }

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


        void OnLightMeasurementValueChanged(object sender, LightIntensityMeasurementEventArgs e)
        {
            var nowait = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
            {
                var m = e.Measurement;
                string caption = "" + Math.Round(m.Lux, 3);
                GetTile("Light").SensorValue = caption;
                connected = true;
            }));
        }


        void OnMovementMeasurementValueChanged(object sender, MovementEventArgs e)
        {
            var nowait = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
            {
                try
                {
                    var m = e.Measurement;
                    string caption = Math.Round(m.AccelX, 3) + "," + Math.Round(m.AccelY, 3) + "," + Math.Round(m.AccelZ, 3);
                    GetTile("Accelerometer").SensorValue = caption;
                    caption = Math.Round(m.GyroX, 3) + "," + Math.Round(m.GyroY, 3) + "," + Math.Round(m.GyroZ, 3);
                    GetTile("Gyroscope").SensorValue = caption;
                    caption = Math.Round(m.MagX, 3) + "," + Math.Round(m.MagY, 3) + "," + Math.Round(m.MagZ, 3);
                    GetTile("Magnetometer").SensorValue = caption;
                    connected = true;
                }
                catch
                {
                }
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

                var unit = (PressureUnit)Settings.Instance.PressureUnit;

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
        public async void Show()
        {
            if (!active)
            {
                Clear();
                active = true;
                await ConnectSensors();
            }
        }

        bool connecting;

        private async Task ConnectSensors()
        {
            try
            {
                if (sensor == null)
                {
                    // no paired SensorTag, tell the user 
                    DisplayMessage("This page should be navigated to with a SensorTag parameter");
                    return;
                }
                if (connecting)
                {
                    return;
                }
                connecting = true;
                if (sensor.Connected || await sensor.ConnectAsync())
                {
                    connected = true;
                    await RegisterEvents(true);
                    if (sensor.Accelerometer != null)
                    {
                        await sensor.Accelerometer.SetPeriod(1000); // save battery
                    }
                    if (sensor.Movement != null)
                    {
                        await sensor.Movement.SetPeriod(1000); // save battery
                    }
                    SensorList.ItemsSource = tiles;
                }

            }
            catch (Exception ex)
            {
                DisplayMessage("Connect failed, please ensure sensor is on and is not in use on another machine.");
                Debug.WriteLine(ex.Message);
            }
            connecting = false;
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
                if (sensor != null)
                {
                    sensor.Disconnect();
                }
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
            Frame frame = Window.Current.Content as Frame;

            switch (model.Caption)
            {
                case "Barometer":
                    frame.Navigate(typeof(PressurePage));
                    break;
                case "Accelerometer":
                    frame.Navigate(typeof(AccelerometerPage));
                    break;
                case "Gyroscope":
                    frame.Navigate(typeof(GyroPage));
                    break;
                case "Humidity":
                    frame.Navigate(typeof(HumidityPage));
                    break;
                case "IR Temperature":
                    frame.Navigate(typeof(TemperaturePage));
                    break;
                case "Magnetometer":
                    frame.Navigate(typeof(MagnetometerPage));
                    break;
                case "Buttons":
                    frame.Navigate(typeof(ButtonPage));
                    break;
                case "Light":
                    frame.Navigate(typeof(LightPage));
                    break;
            }
        }


        private void AddTile(TileModel model)
        {
            if (!(from t in tiles where t.Caption == model.Caption select t).Any())
            {
                tiles.Add(model);
            }
        }

        private void RemoveTiles(IEnumerable<TileModel> enumerable)
        {
            foreach (TileModel tile in enumerable.ToArray())
            {
                tiles.Remove(tile);
            }
        }

        private void OnGoBack(object sender, RoutedEventArgs e)
        {
            Frame.GoBack();
        }
    }
}
