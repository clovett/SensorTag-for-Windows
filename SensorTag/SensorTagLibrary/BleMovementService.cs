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

    [FlagsAttribute]
    public enum MovementFlags
    {
        None = 0,

        /// <summary>
        /// Enable Gyro X-Axis
        /// </summary>
        GyroX = 1,

        /// <summary>
        /// Enable Gyro Y-Axis
        /// </summary>
        GyroY = 2,

        /// <summary>
        /// Enable Gyro Z-Axis
        /// </summary>
        GyroZ = 4,

        /// <summary>
        /// Enable Accelerometer X-Axis
        /// </summary>
        AccelX = 8,
        /// <summary>
        /// Enable Accelerometer Y-Axis
        /// </summary>
        /// 
        AccelY = 0x10,

        /// <summary>
        /// Enable Accelerometer Z-Axis
        /// </summary>
        AccelZ = 0x20,

        /// <summary>
        /// Enable Magnetometer 
        /// </summary>
        Mag = 0x40,

        /// <summary>
        /// The Wake-On-Motion (WOM) feature allows the MPU to operate with only the accelerometer enabled, but will give an interrupt to the CC2650 when motion is detected. 
        /// After a shake is detected, the SensorTag will provide movement data for 10 seconds before entering the MPU re-enters low power WOM state
        /// </summary>
        WakeOnMotion = 0x80,

        /// <summary>
        /// Accelerometer range (2G)
        /// </summary>
        Accel2G = 0,

        /// <summary>
        /// Accelerometer range (4G)
        /// </summary>
        Accel4G = 0x100,

        /// <summary>
        /// Accelerometer range (8G)
        /// </summary>
        Accel8G = 0x200,

        /// <summary>
        /// Accelerometer range (16G)
        /// </summary>
        Accel16G = 0x300 
    }

    /// <summary>
    /// This class provides access to the SensorTag Movement BLE data
    /// </summary>
    public class BleMovementService : BleGenericGattService
    {

        public BleMovementService() 
        {
        }

        /// <summary>
        /// The version of the SensorTag device.  2=CC2650.  Version 1 does not support this service.
        /// </summary>
        public int Version { get; set; }

        static Guid MovementServiceUUid = Guid.Parse("f000aa80-0451-4000-b000-000000000000");
        static Guid MovementCharacteristicUuid = Guid.Parse("f000aa81-0451-4000-b000-000000000000");
        static Guid MovementCharacteristicConfigUuid = Guid.Parse("f000aa82-0451-4000-b000-000000000000");
        static Guid MovementCharacteristicPeriodUuid = Guid.Parse("f000aa83-0451-4000-b000-000000000000");
        
        Delegate _movementValueChanged;

        public event EventHandler<MovementEventArgs> MovementMeasurementValueChanged
        {
            add
            {
                if (_movementValueChanged != null)
                {
                    _movementValueChanged = Delegate.Combine(_movementValueChanged, value);
                }
                else
                {
                    _movementValueChanged = value;
                    RegisterForValueChangeEvents(MovementCharacteristicUuid);
                }
            }
            remove
            {
                if (_movementValueChanged != null)
                {
                    _movementValueChanged = Delegate.Remove(_movementValueChanged, value);
                    if (_movementValueChanged == null)
                    {
                        UnregisterForValueChangeEvents(MovementCharacteristicUuid);
                    }
                }
            }
        }

        private async Task<MovementFlags> GetConfig()
        {
            var ch = GetCharacteristic(MovementCharacteristicConfigUuid);
            if (ch != null)
            {
                var properties = ch.CharacteristicProperties;

                if ((properties & GattCharacteristicProperties.Read) != 0)
                {
                    var result = await ch.ReadValueAsync();
                    IBuffer buffer = result.Value;
                    DataReader reader = DataReader.FromBuffer(buffer);
                    short value = ReadBigEndian16bit(reader);
                    MovementFlags flags = (MovementFlags)value;
                    Debug.WriteLine("Acceleration config = " + flags);
                    return flags;
                }
            }
            return MovementFlags.None;
        }

        bool isReading;

        public async Task StartReading(MovementFlags flags)
        {
            if (!isReading)
            {
                // One bit for each gyro and accelerometer axis (6), magnetometer (1), wake-on-motion enable (1), accelerometer range (2). 
                // Write any bit combination top enable the desired features
                DataWriter writer = new DataWriter();
                WriteBigEndian16bit(writer, (ushort)flags);
                IBuffer buffer = writer.DetachBuffer();
                await WriteCharacteristicBytes(MovementCharacteristicConfigUuid, buffer);
                isReading = true;
            }
        }

        public async Task StopReading()
        {
            if (isReading)
            {
                // Writing 0x0000 powers the unit off. 
                isReading = false;
                DataWriter writer = new DataWriter();
                WriteBigEndian16bit(writer, 0);
                IBuffer buffer = writer.DetachBuffer();
                await WriteCharacteristicBytes(MovementCharacteristicConfigUuid, buffer);
            }
        }
        
        /// <summary>
        /// Get the rate at which accelerometer is being polled, in milliseconds.  
        /// </summary>
        /// <returns>Returns the value read from the sensor or -1 if something goes wrong.</returns>
        public async Task<int> GetPeriod()
        {
            // Resolution 10 ms. Range 100 ms (0x0A) to 2.55 sec (0xFF). Default 1 second (0x64). 
            byte v = await ReadCharacteristicByte(MovementCharacteristicPeriodUuid, Windows.Devices.Bluetooth.BluetoothCacheMode.Uncached);
            return (int)(v * 10);
        }

        /// <summary>
        /// Set the rate at which accelerometer is being polled, in milliseconds.  
        /// </summary>
        /// <param name="milliseconds">The delay between updates, accurate only to 10ms intervals. Maximum value is 2550.</param>
        public async Task SetPeriod(int milliseconds)
        {
            int delay = milliseconds / 10;
            byte p = (byte)delay;
            if (p < 1)
            {
                p = 1;
            }

            await WriteCharacteristicByte(MovementCharacteristicPeriodUuid, p);
        }

        private void OnMovementMeasurementValueChanged(MovementEventArgs args)
        {
            if (_movementValueChanged != null)
            {
                ((EventHandler<MovementEventArgs>)_movementValueChanged)(this, args);
            }
        }


        public async Task<bool> ConnectAsync(string deviceContainerId)
        {
            if (Version == 1)
            {
                throw new NotSupportedException();
            }
            return await this.ConnectAsync(MovementServiceUUid, deviceContainerId);
        }

        protected override void OnCharacteristicValueChanged(GattCharacteristic sender, GattValueChangedEventArgs eventArgs)
        {
            if (sender.Uuid == MovementCharacteristicUuid)
            {
                if (_movementValueChanged != null)
                {
                    uint dataLength = eventArgs.CharacteristicValue.Length;
                    using (DataReader reader = DataReader.FromBuffer(eventArgs.CharacteristicValue))
                    {
                        if (dataLength == 18)
                        {
                            MovementMeasurement measurement = new MovementMeasurement();

                            short gx = ReadBigEndian16bit(reader);
                            short gy = ReadBigEndian16bit(reader);
                            short gz = ReadBigEndian16bit(reader);
                            short ax = ReadBigEndian16bit(reader);
                            short ay = ReadBigEndian16bit(reader);
                            short az = ReadBigEndian16bit(reader);
                            short mx = ReadBigEndian16bit(reader);
                            short my = ReadBigEndian16bit(reader);
                            short mz = ReadBigEndian16bit(reader);

                            measurement.GyroX = ((double)gx * 500.0) / 65536.0;
                            measurement.GyroY = ((double)gy * 500.0) / 65536.0;
                            measurement.GyroZ = ((double)gz * 500.0) / 65536.0;

                            measurement.AccelX = ((double)ax / 32768);
                            measurement.AccelY = ((double)ay / 32768);
                            measurement.AccelZ = ((double)az / 32768);

                            // on SensorTag CC2650 the conversion to micro tesla's is done in the firmware.
                            measurement.MagX = (double)mx;
                            measurement.MagY = (double)my;
                            measurement.MagZ = (double)mz;

                            OnMovementMeasurementValueChanged(new MovementEventArgs(measurement, eventArgs.Timestamp));
                        }
                    }
                }
            }
        }

    }


    public class MovementMeasurement
    {
        /// <summary>
        /// Get/Set X accelerometer in units of 1 g (9.81 m/s^2).
        /// </summary>
        public double AccelX { get; set;}   
        
        /// <summary>
        /// Get/Set Y accelerometer in units of 1 g (9.81 m/s^2).
        /// </summary>
        public double AccelY { get; set;}        
        
        /// <summary>
        /// Get/Set Z accelerometer in units of 1 g (9.81 m/s^2).
        /// </summary>
        public double AccelZ { get; set;}

        /// <summary>
        /// Get/Set X twist in degrees per second.
        /// </summary>
        public double GyroX { get; set; }

        /// <summary>
        /// Get/Set Y twist in degrees per second.
        /// </summary>
        public double GyroY { get; set; }

        /// <summary>
        /// Get/Set Z twist in degrees per second.
        /// </summary>
        public double GyroZ { get; set; }

        /// <summary>
        /// Get/Set X direction in units of 1 micro tesla.
        /// </summary>
        public double MagX { get; set; }

        /// <summary>
        /// Get/Set Y direction in units of 1 micro tesla.
        /// </summary>
        public double MagY { get; set; }

        /// <summary>
        /// Get/Set Z direction in units of 1 micro tesla.
        /// </summary>
        public double MagZ { get; set; }

        public MovementMeasurement()
        {
        }

    }

    public class MovementEventArgs : EventArgs
    {
        public MovementEventArgs(MovementMeasurement measurement, DateTimeOffset timestamp)
        {
            Measurement = measurement;
            Timestamp = timestamp;
        }

        public MovementMeasurement Measurement
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
