using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;
using Buffer = Windows.Storage.Streams.Buffer;

namespace SensorTag
{
    /// <summary>
    /// This class provides access to the SensorTag Humidity BLE data.  The driver for this sensor is using a state machine 
    /// so when the enable command is issued, the sensor starts to perform one measurements and the data is stored. To obtain 
    /// the data either use notifications or read the data directly. The humidity and temperature data in the sensor is issued 
    /// and measured explicitly where the humidity data takes ~64ms to measure. The update rate is one second.
    /// </summary>
    public class BleHumidityService : BleGenericGattService
    {

        public BleHumidityService() 
        {
        }

        static Guid HumidityServiceUuid = Guid.Parse("f000aa20-0451-4000-b000-000000000000");
        static Guid HumidityCharacteristicUuid = Guid.Parse("f000aa21-0451-4000-b000-000000000000");
        static Guid HumidityCharacteristicConfigUuid = Guid.Parse("f000aa22-0451-4000-b000-000000000000");
        
        Delegate _humidityValueChanged;

        public event EventHandler<HumidityMeasurementEventArgs> HumidityMeasurementValueChanged
        {
            add
            {
                if (_humidityValueChanged != null)
                {
                    _humidityValueChanged = Delegate.Combine(_humidityValueChanged, value);
                }
                else
                {
                    _humidityValueChanged = value;
                    RegisterForValueChangeEvents(HumidityCharacteristicUuid);
                }
            }
            remove
            {
                if (_humidityValueChanged != null)
                {
                    _humidityValueChanged = Delegate.Remove(_humidityValueChanged, value);
                    if (_humidityValueChanged == null)
                    {
                        UnregisterForValueChangeEvents(HumidityCharacteristicUuid);
                    }
                }
            }
        }

        private async Task<int> GetConfig()
        {
            var ch = GetCharacteristic(HumidityCharacteristicConfigUuid);
            if (ch != null)
            {
                var properties = ch.CharacteristicProperties;

                if ((properties & GattCharacteristicProperties.Read) != 0)
                {
                    var result = await ch.ReadValueAsync();
                    IBuffer buffer = result.Value;
                    DataReader reader = DataReader.FromBuffer(buffer);
                    var value = reader.ReadByte();
                    Debug.WriteLine("Humidity config = " + value);
                    return (int)value;
                }
            }
            return -1;
        }

        bool isReading;

        public async Task StartReading()
        {
            if (!isReading)
            {
                await WriteCharacteristicByte(HumidityCharacteristicConfigUuid, 1);
                isReading = true;
            }
        }

        public async Task StopReading()
        {
            if (isReading)
            {
                isReading = false;
                await WriteCharacteristicByte(HumidityCharacteristicConfigUuid, 0);
            }
        }
        

        private void OnHumidityMeasurementValueChanged(HumidityMeasurementEventArgs args)
        {
            if (_humidityValueChanged != null)
            {
                ((EventHandler<HumidityMeasurementEventArgs>)_humidityValueChanged)(this, args);
            }
        }

        public async Task<bool> ConnectAsync()
        {
            return await this.ConnectAsync(HumidityServiceUuid, null);
        }

        protected override void OnCharacteristicValueChanged(GattCharacteristic sender, GattValueChangedEventArgs eventArgs)
        {
            if (sender.Uuid == HumidityCharacteristicUuid)
            {
                if (_humidityValueChanged != null)
                {
                    uint dataLength = eventArgs.CharacteristicValue.Length;
                    using (DataReader reader = DataReader.FromBuffer(eventArgs.CharacteristicValue))
                    {
                        if (dataLength == 4)
                        {
                            var data = new byte[dataLength];
                            reader.ReadBytes(data);

                            HumidityMeasurement measurement = new HumidityMeasurement();

                            int temp = (int)data[0] + (data[1] << 8); // upper byte is unsigned.
                            int humidity = (int)data[2] + (data[3] << 8); // upper byte is unsigned.

                            measurement.SetRawTemp(temp);
                            measurement.SetRawHumidity(humidity);

                            OnHumidityMeasurementValueChanged(new HumidityMeasurementEventArgs(measurement, eventArgs.Timestamp));
                        }
                    }
                }
            }
        }

    }


    public class HumidityMeasurement
    {
        /// <summary>
        /// Relative humidity (%RH)
        /// </summary>
        public double Humidity { get; set;}   
        
        /// <summary>
        /// Temperature in Celcius
        /// </summary>
        public double Temperature { get; set; }        
        

        public HumidityMeasurement()
        {
        }


        internal void SetRawTemp(int temp)
        {
            // calculate temperature [deg C] 
            this.Temperature = -46.85 + (175.72 * (double)temp) / 65536.0;
        }

        internal void SetRawHumidity(int humidity)
        {
            humidity &= ~0x0003; // clear bits [1..0] (status bits)
            this.Humidity = -6.0 + (125.0  * (double)humidity) / 65536.0; // RH= -6 + 125 * SRH/2^16
        }
    }

    public class HumidityMeasurementEventArgs : EventArgs
    {
        public HumidityMeasurementEventArgs(HumidityMeasurement measurement, DateTimeOffset timestamp)
        {
            Measurement = measurement;
            Timestamp = timestamp;
        }

        public HumidityMeasurement Measurement
        {
            get;
            private set;
        }

        public DateTimeOffset Timestamp
        {
            get;
            private set;
        }
    }

}
