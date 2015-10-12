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
    /// This class provides access to the SensorTag Magnetometer BLE data
    /// </summary>
    public class BleMagnetometerService : BleGenericGattService
    {

        public BleMagnetometerService() 
        {
        }

        /// <summary>
        /// The version of the SensorTag device.  1=CC2541, 2=CC2650.
        /// </summary>
        public int Version { get; set; }

        static Guid MagnetometerServiceUuid = Guid.Parse("f000aa30-0451-4000-b000-000000000000");
        static Guid MagnetometerCharacteristicUuid = Guid.Parse("f000aa31-0451-4000-b000-000000000000");
        static Guid MagnetometerCharacteristicConfigUuid = Guid.Parse("f000aa32-0451-4000-b000-000000000000");
        static Guid MagnetometerCharacteristicPeriodUuid = Guid.Parse("f000aa33-0451-4000-b000-000000000000");
        
        Delegate _magnetometerValueChanged;

        public event EventHandler<MagnetometerMeasurementEventArgs> MagnetometerMeasurementValueChanged
        {
            add
            {
                if (_magnetometerValueChanged != null)
                {
                    _magnetometerValueChanged = Delegate.Combine(_magnetometerValueChanged, value);
                }
                else
                {
                    _magnetometerValueChanged = value;
                    RegisterForValueChangeEvents(MagnetometerCharacteristicUuid);
                }
            }
            remove
            {
                if (_magnetometerValueChanged != null)
                {
                    _magnetometerValueChanged = Delegate.Remove(_magnetometerValueChanged, value);
                    if (_magnetometerValueChanged == null)
                    {
                        UnregisterForValueChangeEvents(MagnetometerCharacteristicUuid);
                    }
                }
            }
        }

        private async Task<int> GetConfig()
        {
            var ch = GetCharacteristic(MagnetometerCharacteristicConfigUuid);
            if (ch != null)
            {
                var properties = ch.CharacteristicProperties;

                if ((properties & GattCharacteristicProperties.Read) != 0)
                {
                    var result = await ch.ReadValueAsync();
                    IBuffer buffer = result.Value;
                    DataReader reader = DataReader.FromBuffer(buffer);
                    var value = reader.ReadByte();
                    Debug.WriteLine("Magnetometer config = " + value);
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
                await WriteCharacteristicByte(MagnetometerCharacteristicConfigUuid, 1);
                isReading = true;
            }
        }

        public async Task StopReading()
        {
            if (isReading)
            {
                isReading = false;
                await WriteCharacteristicByte(MagnetometerCharacteristicConfigUuid, 0);
            }
        }
        
        /// <summary>
        /// Get the rate at which magnetometer is being polled, in milliseconds.  
        /// </summary>
        /// <returns>Returns the value read from the sensor or -1 if something goes wrong.</returns>
        public async Task<int> GetPeriod()
        {
            byte v = await ReadCharacteristicByte(MagnetometerCharacteristicPeriodUuid, Windows.Devices.Bluetooth.BluetoothCacheMode.Uncached);
            return (int)(v * 10);
        }

        /// <summary>
        /// Set the rate at which magnetometer is being polled, in milliseconds.  
        /// </summary>
        /// <param name="milliseconds">The delay between updates, accurate only to 10ms intervals, default is 2 seconds. </param>
        public async void SetPeriod(int milliseconds)
        {
            int delay = milliseconds / 10;
            if (delay < 0)
            {
                delay = 1;
            }

            await WriteCharacteristicByte(MagnetometerCharacteristicPeriodUuid, (byte)delay);
        }

        private void OnMagnetometerMeasurementValueChanged(MagnetometerMeasurementEventArgs args)
        {
            if (_magnetometerValueChanged != null)
            {
                ((EventHandler<MagnetometerMeasurementEventArgs>)_magnetometerValueChanged)(this, args);
            }
        }


        public async Task<bool> ConnectAsync(string deviceContainerId)
        {
            return await this.ConnectAsync(MagnetometerServiceUuid, deviceContainerId);
        }

        protected override void OnCharacteristicValueChanged(GattCharacteristic sender, GattValueChangedEventArgs eventArgs)
        {
            if (sender.Uuid == MagnetometerCharacteristicUuid)
            {
                if (_magnetometerValueChanged != null)
                {
                    uint dataLength = eventArgs.CharacteristicValue.Length;
                    using (DataReader reader = DataReader.FromBuffer(eventArgs.CharacteristicValue))
                    {
                        if (dataLength == 6)
                        {
                            var data = new byte[dataLength];
                            reader.ReadBytes(data);

                            MagnetometerMeasurement measurement = new MagnetometerMeasurement();

                            int x = (int)data[0] + ((sbyte)data[1] << 8);
                            int y = (int)data[2] + ((sbyte)data[3] << 8);
                            int z = (int)data[4] + ((sbyte)data[5] << 8);

                            measurement.X = (double)x * (2000.0 / 65536.0);
                            measurement.Y = (double)y * (2000.0 / 65536.0);
                            measurement.Z = (double)z * (2000.0 / 65536.0);

                            OnMagnetometerMeasurementValueChanged(new MagnetometerMeasurementEventArgs(measurement, eventArgs.Timestamp));
                        }
                    }
                }
            }
        }
    }


    public class MagnetometerMeasurement
    {
        /// <summary>
        /// Get/Set X direction in units of 1 micro tesla.
        /// </summary>
        public double X { get; set;}   
        
        /// <summary>
        /// Get/Set Y direction in units of 1 micro tesla.
        /// </summary>
        public double Y { get; set;}        
        
        /// <summary>
        /// Get/Set Z direction in units of 1 micro tesla.
        /// </summary>
        public double Z { get; set;}

        public MagnetometerMeasurement()
        {
        }

    }

    public class MagnetometerMeasurementEventArgs : EventArgs
    {
        public MagnetometerMeasurementEventArgs(MagnetometerMeasurement measurement, DateTimeOffset timestamp)
        {
            Measurement = measurement;
            Timestamp = timestamp;
        }

        public MagnetometerMeasurement Measurement
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
