using Microsoft.MobileLabs.Bluetooth;
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
    /// This class provides access to the SensorTag Accelerometer BLE data
    /// </summary>
    public class BleAccelerometerService : BleGenericGattService
    {

        public BleAccelerometerService() 
        {
        }

        static Guid AccelerometerServiceUuid = Guid.Parse("f000aa10-0451-4000-b000-000000000000");
        static Guid AccelerometerCharacteristicUuid = Guid.Parse("f000aa11-0451-4000-b000-000000000000");
        static Guid AccelerometerCharacteristicConfigUuid = Guid.Parse("f000aa12-0451-4000-b000-000000000000");
        static Guid AccelerometerCharacteristicPeriodUuid = Guid.Parse("f000aa13-0451-4000-b000-000000000000");
        
        Delegate _accelerometerValueChanged;

        public event EventHandler<AccelerometerMeasurementEventArgs> AccelerometerMeasurementValueChanged
        {
            add
            {
                if (_accelerometerValueChanged != null)
                {
                    _accelerometerValueChanged = Delegate.Combine(_accelerometerValueChanged, value);
                }
                else
                {
                    _accelerometerValueChanged = value;
                    RegisterForValueChangeEvents(AccelerometerCharacteristicUuid);
                }
            }
            remove
            {
                if (_accelerometerValueChanged != null)
                {
                    _accelerometerValueChanged = Delegate.Remove(_accelerometerValueChanged, value);
                }
                if (_accelerometerValueChanged == null)
                {
                    UnregisterForValueChangeEvents(AccelerometerCharacteristicUuid);
                }
            }
        }

        private async Task<int> GetConfig()
        {
            var ch = GetCharacteristic(AccelerometerCharacteristicConfigUuid);
            if (ch != null)
            {
                var properties = ch.CharacteristicProperties;

                if ((properties & GattCharacteristicProperties.Read) != 0)
                {
                    var result = await ch.ReadValueAsync();
                    IBuffer buffer = result.Value;
                    DataReader reader = DataReader.FromBuffer(buffer);
                    var value = reader.ReadByte();
                    Debug.WriteLine("Acceleration config = " + value);
                    return (int)value;
                }
            }
            return -1;
        }

        public async void StartReading()
        {
            await WriteCharacteristicByte(AccelerometerCharacteristicConfigUuid, 1);
        }

        public async void StopReading()
        {
            await WriteCharacteristicByte(AccelerometerCharacteristicConfigUuid, 0);
        }
        
        /// <summary>
        /// Get the rate at which accelerometer is being polled, in milliseconds.  
        /// </summary>
        /// <returns>Returns the value read from the sensor or -1 if something goes wrong.</returns>
        public async Task<int> GetPeriod()
        {
            byte v = await ReadCharacteristicByte(AccelerometerCharacteristicPeriodUuid, Windows.Devices.Bluetooth.BluetoothCacheMode.Uncached);
            return (int)(v * 10);
        }

        /// <summary>
        /// Set the rate at which accelerometer is being polled, in milliseconds.  
        /// </summary>
        /// <param name="milliseconds">The delay between updates, accurate only to 10ms intervals. </param>
        public async Task SetPeriod(int milliseconds)
        {
            int delay = milliseconds / 10;
            if (delay < 0)
            {
                delay = 1;
            }

            await WriteCharacteristicByte(AccelerometerCharacteristicPeriodUuid, (byte)delay);
        }

        private void OnAccelerationMeasurementValueChanged(AccelerometerMeasurementEventArgs args)
        {
            if (_accelerometerValueChanged != null)
            {
                ((EventHandler<AccelerometerMeasurementEventArgs>)_accelerometerValueChanged)(this, args);
            }
        }


        public async Task<bool> ConnectAsync()
        {
            return await this.ConnectAsync(AccelerometerServiceUuid, null);
        }

        protected override void OnCharacteristicValueChanged(GattCharacteristic sender, GattValueChangedEventArgs eventArgs)
        {
            if (sender.Uuid == AccelerometerCharacteristicUuid)
            {
                if (_accelerometerValueChanged != null)
                {
                    uint dataLength = eventArgs.CharacteristicValue.Length;
                    using (DataReader reader = DataReader.FromBuffer(eventArgs.CharacteristicValue))
                    {
                        if (dataLength == 3)
                        {
                            var data = new byte[dataLength];
                            reader.ReadBytes(data);

                            AccelerometerMeasurement measurement = new AccelerometerMeasurement();

                            sbyte x = (sbyte)data[0];
                            sbyte y = (sbyte)data[1];
                            sbyte z = (sbyte)data[2];

                            measurement.X = (double)x / 64.0;
                            measurement.Y = (double)y / 64.0;
                            measurement.Z = (double)z / 64.0;

                            OnAccelerationMeasurementValueChanged(new AccelerometerMeasurementEventArgs(measurement, eventArgs.Timestamp));
                        }
                    }
                }
            }
        }

    }


    public class AccelerometerMeasurement
    {
        /// <summary>
        /// Get/Set X accelerometer in units of 1 g (9.81 m/s^2).
        /// </summary>
        public double X { get; set;}   
        
        /// <summary>
        /// Get/Set Y accelerometer in units of 1 g (9.81 m/s^2).
        /// </summary>
        public double Y { get; set;}        
        
        /// <summary>
        /// Get/Set Z accelerometer in units of 1 g (9.81 m/s^2).
        /// </summary>
        public double Z { get; set;}

        public AccelerometerMeasurement()
        {
        }

    }

    public class AccelerometerMeasurementEventArgs : EventArgs
    {
        public AccelerometerMeasurementEventArgs(AccelerometerMeasurement measurement, DateTimeOffset timestamp)
        {
            Measurement = measurement;
            Timestamp = timestamp;
        }

        public AccelerometerMeasurement Measurement
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
