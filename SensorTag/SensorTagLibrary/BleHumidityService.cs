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

        /// <summary>
        /// The version of the SensorTag device.  1=CC2541, 2=CC2650.
        /// </summary>
        public int Version { get; set; }

        static Guid HumidityServiceUuid = Guid.Parse("f000aa20-0451-4000-b000-000000000000");
        static Guid HumidityCharacteristicUuid = Guid.Parse("f000aa21-0451-4000-b000-000000000000");
        static Guid HumidityCharacteristicConfigUuid = Guid.Parse("f000aa22-0451-4000-b000-000000000000");

        // Period is only supported on version 2
        static Guid HumidityCharacteristicPeriodUuid = Guid.Parse("f000aa23-0451-4000-b000-000000000000");

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
                    byte value = await ReadCharacteristicByte(HumidityCharacteristicConfigUuid, Windows.Devices.Bluetooth.BluetoothCacheMode.Uncached);
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


        /// <summary>
        /// Get the rate at which humidity is being polled, in milliseconds.  
        /// This is only supported on Version 2 of the sensor
        /// </summary>
        /// <returns>Returns the value read from the sensor or -1 if something goes wrong.</returns>
        public async Task<int> GetPeriod()
        {
            if (Version == 2)
            {
                byte v = await ReadCharacteristicByte(HumidityCharacteristicPeriodUuid, Windows.Devices.Bluetooth.BluetoothCacheMode.Uncached);
                return (int)(v * 10);
            }
            return 1000;
        }

        /// <summary>
        /// Set the rate at which humidity is being polled, in milliseconds.  
        /// </summary>
        /// <param name="milliseconds">The delay between updates, accurate only to 10ms intervals. Maximum value is 2550.</param>
        public async Task SetPeriod(int milliseconds)
        {
            if (Version == 2)
            {
                int delay = milliseconds / 10;
                byte p = (byte)delay;
                if (p < 1)
                {
                    p = 1;
                }

                await WriteCharacteristicByte(HumidityCharacteristicPeriodUuid, p);
            }
        }

        private void OnHumidityMeasurementValueChanged(HumidityMeasurementEventArgs args)
        {
            if (_humidityValueChanged != null)
            {
                ((EventHandler<HumidityMeasurementEventArgs>)_humidityValueChanged)(this, args);
            }
        }

        public async Task<bool> ConnectAsync(string deviceContainerId)
        {
            return await this.ConnectAsync(HumidityServiceUuid, deviceContainerId);
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
                            ushort temp = ReadBigEndianU16bit(reader);
                            ushort humidity = ReadBigEndianU16bit(reader);

                            var measurement = new HumidityMeasurement();

                            if (Version == 1)
                            {
                                // calculate temperature [deg C] 
                                measurement.Temperature = -46.85 + (175.72 * (double)temp) / 65536.0;

                                humidity &= 0xFFFC; // clear bits [1..0] (status bits)
                                measurement.Humidity = -6.0 + (125.0 * (double)humidity) / 65536.0; // RH= -6 + 125 * SRH/2^16
                            }
                            else
                            {
                                measurement.Temperature = ((double)temp / 65536.0) * 165 - 40;
                                measurement.Humidity = ((double)humidity / 65536.0) * 100;
                            }

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
        public double Humidity { get; set; }

        /// <summary>
        /// Temperature in Celcius
        /// </summary>
        public double Temperature { get; set; }


        public HumidityMeasurement()
        {
        }


        internal void SetRawHumidity(int humidity)
        {
            humidity &= ~0x0003; // clear bits [1..0] (status bits)
            this.Humidity = -6.0 + (125.0 * (double)humidity) / 65536.0; // RH= -6 + 125 * SRH/2^16
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
