using Microsoft.MobileLabs.Bluetooth;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace SensorTag
{
    public class SensorTag : INotifyPropertyChanged
    {
        BleIRTemperatureService _tempService;
        BleButtonService _buttonService;
        BleAccelerometerService _accelService;
        BleGyroscopeService _gyroService;
        BleMagnetometerService _magService;
        BleHumidityService _humidityService;
        BleBarometerService _barometerService;
        string deviceName;

        static SensorTag _instance;

        public static SensorTag Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new SensorTag();
                }
                return _instance;
            }
        }

        public string DeviceName
        {
            get { return this.deviceName; }
            set
            {
                if (this.deviceName != value)
                {
                    this.deviceName = value;
                    OnPropertyChanged("DeviceName");
                }
            }
        }

        public BleAccelerometerService Accelerometer { get { return _accelService; } }
        public BleGyroscopeService Gyroscope { get { return _gyroService; } }
        public BleMagnetometerService Magnetometer { get { return _magService; } }
        public BleIRTemperatureService IRTemperature { get { return _tempService; } }
        public BleButtonService Buttons { get { return _buttonService; } }
        public BleHumidityService Humidity { get { return _humidityService; } }
        public BleBarometerService Barometer { get { return _barometerService; } }


        public void Disconnect()
        {
            disconnecting = true;

            if (_tempService != null)
            {
                using (_tempService)
                {
                    try
                    {
                        _tempService.Error -= OnServiceError;
                        _tempService.ConnectionChanged -= OnConnectionChanged;
                        _tempService.StopReading();
                    }
                    catch { }
                    _tempService = null;
                }
            }
            if (_buttonService != null)
            {
                using (_buttonService)
                {
                    _buttonService.Error -= OnServiceError;
                    _buttonService.ConnectionChanged -= OnConnectionChanged;
                    _buttonService = null;
                }
            }
            if (_accelService != null)
            {
                using (_accelService)
                {
                    try
                    {
                        _accelService.Error -= OnServiceError;
                        _accelService.ConnectionChanged -= OnConnectionChanged;
                        _accelService.StopReading();
                    }
                    catch { }
                    _accelService = null;
                }
            }

            if (_gyroService != null)
            {
                using (_gyroService)
                {
                    try
                    {
                        _gyroService.Error -= OnServiceError;
                        _gyroService.ConnectionChanged -= OnConnectionChanged;
                        _gyroService.StopReading();
                    }
                    catch { }
                    _gyroService = null;
                }
            }
            if (_magService != null)
            {
                using (_magService)
                {
                    try
                    {
                        _magService.Error -= OnServiceError;
                        _magService.ConnectionChanged -= OnConnectionChanged;
                        _magService.StopReading();
                    }
                    catch { }
                    _magService = null;
                }
            }

            if (_humidityService != null)
            {
                using (_humidityService)
                {
                    try
                    {
                        _humidityService.Error -= OnServiceError;
                        _humidityService.ConnectionChanged -= OnConnectionChanged;
                        _humidityService.StopReading();
                    }
                    catch { }
                    _humidityService = null;
                }
            }

            if (_barometerService != null)
            {
                using (_barometerService)
                {
                    try
                    {
                        _barometerService.Error -= OnServiceError;
                        _barometerService.ConnectionChanged -= OnConnectionChanged;
                        _barometerService.StopReading();
                    }
                    catch { }
                    _barometerService = null;
                }
            }
        }

        public event EventHandler<string> StatusChanged;

        private void OnStatusChanged(string status)
        {
            if (StatusChanged != null)
            {
                StatusChanged(this, status);
            }
        }

        bool connecting;
        bool disconnecting;

        public async Task Reconnect()
        {
            if (!connecting)
            {
                disconnecting = false;

                try
                {
                    connecting = true;
                    OnStatusChanged("connecting...");

                    // since all this code is async, user could quit in the middle, hence all the checks
                    // on the "disconnecting" state.
                    await ConnectIRTemperatureService();
                    if (disconnecting) return;
                    await ConnectButtonService();
                    if (disconnecting) return;
                    await ConnectAccelerometerService();
                    if (disconnecting) return;
                    await ConnectGyroscopeService();
                    if (disconnecting) return;
                    await ConnectMagnetometerService();
                    if (disconnecting) return;
                    await ConnectHumidityService();
                    if (disconnecting) return;
                    await ConnectBarometerService();
                    if (disconnecting) return;
                    OnStatusChanged("connected");
                }
                catch (Exception ex)
                {
                    OnStatusChanged(ex.Message);
                }
                finally
                {
                    connecting = false;
                }

            }
        }

        private async Task ConnectIRTemperatureService()
        {
            if (_tempService == null)
            {
                _tempService = new BleIRTemperatureService();
                _tempService.Error += OnServiceError;

                if (await _tempService.ConnectAsync())
                {
                    this.DeviceName = _tempService.DeviceName;
                    _tempService.ConnectionChanged += OnConnectionChanged;
                }
            }
        }

        private async Task ConnectButtonService()
        {
            if (_buttonService == null)
            {
                _buttonService = new BleButtonService();
                _buttonService.Error += OnServiceError;

                if (await _buttonService.ConnectAsync())
                {
                    this.DeviceName = _buttonService.DeviceName;
                    _buttonService.ConnectionChanged += OnConnectionChanged;
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
                    DeviceName = "" + _accelService.DeviceName;
                    _accelService.ConnectionChanged += OnConnectionChanged;
                    _accelService.StopReading();
                    //_accelService.SetPeriod(100); // 10 times a second.
                    //int interval = await _accelService.GetPeriod();
                    //Debug.WriteLine("Reading acceleration every " + interval + "ms");
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
                    DeviceName = "" + _gyroService.DeviceName;
                    _gyroService.ConnectionChanged += OnConnectionChanged;

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
                    DeviceName = "" + _magService.DeviceName;
                    _magService.ConnectionChanged += OnConnectionChanged;

                    //int interval = await _magService.GetPeriod();
                    //Debug.WriteLine("Reading magnetometer every " + interval + "ms");
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
                    DeviceName = "" + _humidityService.DeviceName;
                    _humidityService.ConnectionChanged += OnConnectionChanged;
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
                    DeviceName = "" + _barometerService.DeviceName;
                    OnStatusChanged("calibrating...");
                    _barometerService.ConnectionChanged += OnConnectionChanged;
                    await _barometerService.StartCalibration();
                    OnStatusChanged("calibrated");
                }
            }
        }

        public event EventHandler<string> ServiceError;

        private void OnServiceError(object sender, string message)
        {
            if (ServiceError != null)
            {
                ServiceError(sender, message);
            }

        }


        public event EventHandler<ConnectionChangedEventArgs> ConnectionChanged;

        void OnConnectionChanged(object sender, ConnectionChangedEventArgs e)
        {
            if (ConnectionChanged != null)
            {
                ConnectionChanged(sender, e);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
