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
    public enum GyroscopeAxes
    {
        None = 0,
        X = 1,
        Y = 2,
        XY = 3,
        Z = 4,
        XZ = 5,
        YZ = 6,
        XYZ = 7
    }

    /// <summary>
    /// This class provides access to the SensorTag Gyroscope BLE data
    /// </summary>
    public class BleGyroscopeService : BleGenericGattService
    {

        public BleGyroscopeService() 
        {
        }

        /// <summary>
        /// The version of the SensorTag device.  1=CC2541, 2=CC2650.
        /// </summary>
        public int Version { get; set; }

        static Guid GyroscopeServiceUuid = Guid.Parse("f000aa50-0451-4000-b000-000000000000");
        static Guid GyroscopeCharacteristicUuid = Guid.Parse("f000aa51-0451-4000-b000-000000000000");
        static Guid GyroscopeCharacteristicConfigUuid = Guid.Parse("f000aa52-0451-4000-b000-000000000000");
        
        Delegate _gyroscopeValueChanged;

        public event EventHandler<GyroscopeMeasurementEventArgs> GyroscopeMeasurementValueChanged
        {
            add
            {
                if (_gyroscopeValueChanged != null)
                {
                    _gyroscopeValueChanged = Delegate.Combine(_gyroscopeValueChanged, value);
                }
                else
                {
                    _gyroscopeValueChanged = value;
                    RegisterForValueChangeEvents(GyroscopeCharacteristicUuid);
                }
            }
            remove
            {
                if (_gyroscopeValueChanged != null)
                {
                    _gyroscopeValueChanged = Delegate.Remove(_gyroscopeValueChanged, value);
                    if (_gyroscopeValueChanged == null)
                    {
                        UnregisterForValueChangeEvents(GyroscopeCharacteristicUuid);
                    }
                }
            }
        }

        private async Task<int> GetConfig()
        {
            var ch = GetCharacteristic(GyroscopeCharacteristicConfigUuid);
            if (ch != null)
            {
                var properties = ch.CharacteristicProperties;

                if ((properties & GattCharacteristicProperties.Read) != 0)
                {
                    var result = await ch.ReadValueAsync();
                    IBuffer buffer = result.Value;
                    DataReader reader = DataReader.FromBuffer(buffer);
                    var value = reader.ReadByte();
                    Debug.WriteLine("Gyroscope config = " + value);
                    return (int)value;
                }
            }
            return -1;
        }

        
        GyroscopeAxes readingAxes = GyroscopeAxes.None;

        /// <summary>
        /// Enable getting GyroscopeMeasurementValueChanged events on the given axes.
        /// The gyro produces new values about once per second and that period cannot be changed.
        /// </summary>
        /// <param name="enableAxes"></param>
        public async Task StartReading(GyroscopeAxes enableAxes)
        {
            if (readingAxes != enableAxes)
            {
                byte value = (byte)enableAxes;
                await WriteCharacteristicByte(GyroscopeCharacteristicConfigUuid, value);
                readingAxes = enableAxes;
            }
        }

        public async Task StopReading()
        {
            if (readingAxes != GyroscopeAxes.None)
            {
                readingAxes = GyroscopeAxes.None;
                await WriteCharacteristicByte(GyroscopeCharacteristicConfigUuid, 0);
            }
        }
        
        private void OnGyroscopeMeasurementValueChanged(GyroscopeMeasurementEventArgs args)
        {
            if (_gyroscopeValueChanged != null)
            {
                ((EventHandler<GyroscopeMeasurementEventArgs>)_gyroscopeValueChanged)(this, args);
            }
        }


        public async Task<bool> ConnectAsync(string deviceContainerId)
        {
            return await this.ConnectAsync(GyroscopeServiceUuid, deviceContainerId);
        }

        protected override void OnCharacteristicValueChanged(GattCharacteristic sender, GattValueChangedEventArgs eventArgs)
        {
            if (sender.Uuid == GyroscopeCharacteristicUuid)
            {
                if (_gyroscopeValueChanged != null)
                {
                    uint dataLength = eventArgs.CharacteristicValue.Length;
                    using (DataReader reader = DataReader.FromBuffer(eventArgs.CharacteristicValue))
                    {
                        if (dataLength == 6)
                        {
                            short x = ReadBigEndian16bit(reader);
                            short y = ReadBigEndian16bit(reader);
                            short z = ReadBigEndian16bit(reader);

                            GyroscopeMeasurement measurement = new GyroscopeMeasurement();
                            measurement.X = ((double)x * 500.0) / 65536.0;
                            measurement.Y = ((double)y * 500.0) / 65536.0;
                            measurement.Z = ((double)z * 500.0) / 65536.0;

                            OnGyroscopeMeasurementValueChanged(new GyroscopeMeasurementEventArgs(measurement, eventArgs.Timestamp));
                        }
                    }
                }
            }
        }
    }


    public class GyroscopeMeasurement
    {
        /// <summary>
        /// Get/Set X twist in degrees per second.
        /// </summary>
        public double X { get; set;}   
        
        /// <summary>
        /// Get/Set Y twist in degrees per second.
        /// </summary>
        public double Y { get; set;}        
        
        /// <summary>
        /// Get/Set Z twist in degrees per second.
        /// </summary>
        public double Z { get; set;}

        public GyroscopeMeasurement()
        {
        }

    }

    public class GyroscopeMeasurementEventArgs : EventArgs
    {
        public GyroscopeMeasurementEventArgs(GyroscopeMeasurement measurement, DateTimeOffset timestamp)
        {
            Measurement = measurement;
            Timestamp = timestamp;
        }

        public GyroscopeMeasurement Measurement
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
