using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace SensorTag
{
    /// <summary>
    /// This class combines all of the GATT services provided by SensorTag into one helper class.
    /// See http://processors.wiki.ti.com/index.php/CC2650_SensorTag_User's_Guide#IR_Temperature_Sensor
    /// for details on GATT services.
    /// </summary>
    public class SensorTag : INotifyPropertyChanged
    {
        BleIRTemperatureService _tempService;
        BleButtonService _buttonService;
        BleAccelerometerService _accelService;
        BleGyroscopeService _gyroService;
        BleMagnetometerService _magService;
        BleHumidityService _humidityService;
        BleBarometerService _barometerService;
        bool connected;
        bool connecting;
        bool disconnecting;
        BleGattDeviceInfo deviceInfo;
        int version;

        private SensorTag(BleGattDeviceInfo deviceInfo)
        {
            this.deviceInfo = deviceInfo;
            this.version = 1;
            if (deviceInfo.DeviceInformation.Name == "CC2650 SensorTag")
            {
                this.version = 2;
            }
        }
        private SensorTag()
        {
            throw new InvalidOperationException();
        }

        public bool Connected { get { return connected; } }
        
        /// <summary>
        /// Find all SensorTag devices that are paired with this PC.
        /// </summary>
        /// <returns></returns>
        public static async Task<IEnumerable<SensorTag>> FindAllDevices()
        {
            List<SensorTag> result = new List<SensorTag>();
            foreach (var device in await BleGenericGattService.FindMatchingDevices(BleIRTemperatureService.IRTemperatureServiceUuid))
            {
                if (device.DeviceInformation.Name.Contains("SensorTag"))
                {
                    result.Add(new SensorTag(device));
                }
            }
            return result;
        }


        public string DeviceName
        {
            get { return deviceInfo.DeviceInformation.Name; }
        }

        public BleAccelerometerService Accelerometer { get { return _accelService; } }
        public BleGyroscopeService Gyroscope { get { return _gyroService; } }
        public BleMagnetometerService Magnetometer { get { return _magService; } }
        public BleIRTemperatureService IRTemperature { get { return _tempService; } }
        public BleButtonService Buttons { get { return _buttonService; } }
        public BleHumidityService Humidity { get { return _humidityService; } }
        public BleBarometerService Barometer { get { return _barometerService; } }


        public event EventHandler<string> StatusChanged;

        private void OnStatusChanged(string status)
        {
            if (StatusChanged != null)
            {
                StatusChanged(this, status);
            }
        }

        /// <summary>
        /// Connect or reconnect to the device.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> ConnectAsync()
        {
            if (!connecting && !connected)
            {
                disconnecting = false;

                try
                {
                    OnStatusChanged("connecting...");

                    // since all this code is async, user could quit in the middle, hence all the checks
                    // on the "disconnecting" state.
                    if (!await ConnectIRTemperatureService())
                    {
                        return false;
                    };
                    await ConnectButtonService();
                    if (disconnecting) return false;
                    await ConnectAccelerometerService();
                    if (disconnecting) return false;
                    await ConnectGyroscopeService();
                    if (disconnecting) return false;
                    await ConnectMagnetometerService();
                    if (disconnecting) return false;
                    await ConnectHumidityService();
                    if (disconnecting) return false;
                    await ConnectBarometerService();
                    if (disconnecting) return false;
                    connected = true;
                    OnStatusChanged("connected");
                }
                finally
                {
                    connecting = false;
                }
            }
            return true;
        }

        public async void Disconnect()
        {
            disconnecting = true;
            connected = false;

            if (_tempService != null)
            {
                using (_tempService)
                {
                    try
                    {
                        _tempService.Error -= OnServiceError;
                        _tempService.ConnectionChanged -= OnConnectionChanged;
                        await _tempService.StopReading();
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
                        await _accelService.StopReading();
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
                        await _gyroService.StopReading();
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
                        await _magService.StopReading();
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
                        await _humidityService.StopReading();
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
                        await _barometerService.StopReading();
                    }
                    catch { }
                    _barometerService = null;
                }
            }
        }

        private async Task<bool> ConnectIRTemperatureService()
        {
            if (_tempService == null)
            {
                _tempService = new BleIRTemperatureService() { Version = this.version };
                _tempService.Error += OnServiceError;

                if (await _tempService.ConnectAsync(deviceInfo.ContainerId))
                {
                    _tempService.ConnectionChanged += OnConnectionChanged;
                    return true;
                }
                _tempService.Error -= OnServiceError;
                _tempService = null;
                return false;
            }
            return true;
        }

        private async Task<bool> ConnectButtonService()
        {
            if (_buttonService == null)
            {
                _buttonService = new BleButtonService() { Version = this.version };
                _buttonService.Error += OnServiceError;

                if (await _buttonService.ConnectAsync(deviceInfo.ContainerId))
                {
                    _buttonService.ConnectionChanged += OnConnectionChanged;
                    return true;
                }
                _buttonService.Error -= OnServiceError;
                _buttonService = null;
                return false;
            }
            return true;
        }

        private async Task<bool> ConnectAccelerometerService()
        {
            if (_accelService == null)
            {
                _accelService = new BleAccelerometerService() { Version = this.version };
                _accelService.Error += OnServiceError;

                if (await _accelService.ConnectAsync(deviceInfo.ContainerId))
                {
                    _accelService.ConnectionChanged += OnConnectionChanged;
                    return true;
                }
                _accelService.Error -= OnServiceError;
                _accelService = null;
                return false;
            }
            return true;
        }

        private async Task<bool> ConnectGyroscopeService()
        {
            if (_gyroService == null)
            {
                _gyroService = new BleGyroscopeService() { Version = this.version };
                _gyroService.Error += OnServiceError;

                if (await _gyroService.ConnectAsync(deviceInfo.ContainerId))
                {
                    _gyroService.ConnectionChanged += OnConnectionChanged;
                    return true;
                }

                _gyroService.Error -= OnServiceError;
                _gyroService = null;
                return false;
            }
            return true;
        }


        private async Task<bool> ConnectMagnetometerService()
        {
            if (_magService == null)
            {
                _magService = new BleMagnetometerService() { Version = this.version };
                _magService.Error += OnServiceError;

                if (await _magService.ConnectAsync(deviceInfo.ContainerId))
                {
                    _magService.ConnectionChanged += OnConnectionChanged;
                    return true;
                }
                _magService.Error -= OnServiceError;
                _magService = null;
                return false;
            }
            return true;
        }

        private async Task<bool> ConnectHumidityService()
        {
            if (_humidityService == null)
            {
                _humidityService = new BleHumidityService() { Version = this.version };
                _humidityService.Error += OnServiceError;

                if (await _humidityService.ConnectAsync(deviceInfo.ContainerId))
                {
                    _humidityService.ConnectionChanged += OnConnectionChanged;
                    return true;
                }
                _humidityService.Error -= OnServiceError;
                _humidityService = null;
                return false;
            }
            return true;
        }


        private async Task<bool> ConnectBarometerService()
        {
            if (_barometerService == null)
            {
                _barometerService = new BleBarometerService() { Version = this.version };
                _barometerService.Error += OnServiceError;

                if (await _barometerService.ConnectAsync(deviceInfo.ContainerId))
                {
                    OnStatusChanged("calibrating barometer...");
                    _barometerService.ConnectionChanged += OnConnectionChanged;
                    await _barometerService.StartCalibration();
                    OnStatusChanged("calibrated");
                    return true;
                }
                _barometerService.Error -= OnServiceError;
                _barometerService = null;
                return false;
            }
            return true;
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
