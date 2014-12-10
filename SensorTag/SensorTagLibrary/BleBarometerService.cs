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
    /// This class provides access to the SensorTag Barometric Pressure BLE data.  The driver for this sensor is using a state machine 
    /// so when the enable command is issued, the sensor starts to perform one measurements and the data is stored. To obtain data OTA 
    /// either use notifications or read the data directly. Note that only one notification will be sent since only one measurement is 
    /// performed.
    /// </summary>
    public class BleBarometerService : BleGenericGattService
    {
        int[] c; // calibration data.

        public BleBarometerService() 
        {
        }

        static Guid BarometerServiceUuid = Guid.Parse("f000aa40-0451-4000-b000-000000000000");
        static Guid BarometerCharacteristicUuid = Guid.Parse("f000aa41-0451-4000-b000-000000000000");
        static Guid BarometerCharacteristicConfigUuid = Guid.Parse("f000aa42-0451-4000-b000-000000000000");
        static Guid BarometerCharacteristicCalibrationUuid = Guid.Parse("f000aa43-0451-4000-b000-000000000000");
        static Guid BarometerCharacteristicPeriodUuid = Guid.Parse("f000aa44-0451-4000-b000-000000000000");
        
        Delegate _barometerValueChanged;

        public event EventHandler<BarometerMeasurementEventArgs> BarometerMeasurementValueChanged
        {
            add
            {
                if (_barometerValueChanged != null)
                {
                    _barometerValueChanged = Delegate.Combine(_barometerValueChanged, value);
                }
                else
                {
                    _barometerValueChanged = value;
                    var nowait = RegisterForValueChangeEvents(BarometerCharacteristicUuid);
                }
            }
            remove
            {
                if (_barometerValueChanged != null)
                {
                    _barometerValueChanged = Delegate.Remove(_barometerValueChanged, value);
                }
                if (_barometerValueChanged == null)
                {
                    var nowait = UnregisterForValueChangeEvents(BarometerCharacteristicUuid);
                }
            }
        }

        private async Task<int> GetConfig()
        {
            var ch = GetCharacteristic(BarometerCharacteristicConfigUuid);
            if (ch != null)
            {
                var properties = ch.CharacteristicProperties;

                if ((properties & GattCharacteristicProperties.Read) != 0)
                {
                    var result = await ch.ReadValueAsync();
                    IBuffer buffer = result.Value;
                    DataReader reader = DataReader.FromBuffer(buffer);
                    var value = reader.ReadByte();
                    Debug.WriteLine("Barometer config = " + value);
                    return (int)value;
                }
            }
            return -1;
        }

        public async void StartReading()
        {
            await WriteCharacteristicByte(BarometerCharacteristicConfigUuid, 1);
        }

        public async void StopReading()
        {
            await WriteCharacteristicByte(BarometerCharacteristicConfigUuid, 0);
        }


        public async Task StartCalibration()
        {
            await WriteCharacteristicByte(BarometerCharacteristicConfigUuid, 2);

            await ReadCalibration();

            StartReading();
        }

        public event EventHandler Calibrated;

        /// <summary>
        /// Get the rate at which sensor is being polled, in milliseconds.  
        /// </summary>
        /// <returns>Returns the value read from the sensor or -1 if something goes wrong.</returns>
        public async Task<int> GetPeriod()
        {
            byte v = await ReadCharacteristicByte(BarometerCharacteristicPeriodUuid, Windows.Devices.Bluetooth.BluetoothCacheMode.Uncached);
            return (int)(v * 10);
        }

        /// <summary>
        /// Set the rate at which sensor is being polled.
        /// The period ranges for 100 ms to 2.55 seconds, resolution 10 ms.
        /// </summary>
        /// <param name="milliseconds">The delay between updates, accurate only to 10ms intervals. </param>
        public async void SetPeriod(int milliseconds)
        {
            int delay = milliseconds / 10;
            if (delay < 0)
            {
                delay = 1;
            }
            await WriteCharacteristicByte(BarometerCharacteristicPeriodUuid, (byte)delay);
        }


        private void OnBarometerMeasurementValueChanged(BarometerMeasurementEventArgs args)
        {
            if (_barometerValueChanged != null)
            {
                ((EventHandler<BarometerMeasurementEventArgs>)_barometerValueChanged)(this, args);
            }
        }

        public async Task<bool> ConnectAsync()
        {
            return await this.ConnectAsync(BarometerServiceUuid, null);
        }

        protected override void OnCharacteristicValueChanged(GattCharacteristic sender, GattValueChangedEventArgs eventArgs)
        {
            if (sender.Uuid == BarometerCharacteristicUuid)
            {
                if (_barometerValueChanged != null)
                {
                    uint dataLength = eventArgs.CharacteristicValue.Length;
                    using (DataReader reader = DataReader.FromBuffer(eventArgs.CharacteristicValue))
                    {
                        if (dataLength == 4)
                        {
                            var data = new byte[dataLength];
                            reader.ReadBytes(data);
                            CalcBarometricPressure(eventArgs.Timestamp, data);
                        }
                    }
                }
            }
            else if (sender.Uuid == BarometerCharacteristicCalibrationUuid)
            {
                UpdateCalibrationData(eventArgs);
            }
        }

        private void CalcBarometricPressure(DateTimeOffset timestamp, byte[] data)
        {
            // this code implements the algorithm in T5400 data sheet at
            // http://processors.wiki.ti.com/index.php/SensorTag_User_Guide#Barometric_Pressure_Sensor

            // raw temperature
            int Tr = (int)data[0] + ((sbyte)data[1] << 8); // upper byte is signed.

            // raw pressure
            int Pr = (int)data[2] + (data[3] << 8); // upper byte is unsigned.

            if (this.c != null)
            {
                BarometerMeasurement measurement = new BarometerMeasurement();                

                long c1 = c[0];
                long c2 = c[1];
                long c3 = c[2];
                long c4 = c[3];
                long c5 = c[4];
                long c6 = c[5];
                long c7 = c[6];
                long c8 = c[7];

                // Ta = ((c1 * Tr) / 2^24) + (c2 / 2^10)
                var Ta = ((c1 * Tr) >> 24) + (c2 >> 10);

                // * Formula from application note, rev_X:
                // Sensitivity = (c3 + ((c4 * Tr) / 2^17) + ((c5 * Tr^2) / 2^34))
                // Offset = (c6 * 2^14) + ((c7 * Tr) / 2^3) + ((c8 * Tr^2) / 2^19)
                // Pa = (Sensitivity * Pr + Offset) / 2^14

                var Sensitivity = c3 + ((c4 * Tr) >> 17) + ((c5 * Tr * Tr) >> 34);
                var Offset = (c6 << 14) + ((c7 * Tr) >> 3) + ((c8 * Tr * Tr) >> 19);
                var Pa = (Sensitivity * Pr + Offset) >> 14;

                measurement.Pascals = Pa;

                OnBarometerMeasurementValueChanged(new BarometerMeasurementEventArgs(measurement, timestamp));
            }
        }


        private async Task<int[]> ReadCalibration()
        {
            byte[] data = await ReadCharacteristicBytes(BarometerCharacteristicCalibrationUuid, Windows.Devices.Bluetooth.BluetoothCacheMode.Uncached);
            if (data.Length == 16)
            {
                bool nonZero = false;
                int[] c = new int[8];

                // first 4 values are unsigned shorts
                for (int i = 0; i < 4; i++)
                {
                    int j = i * 2; // byte index.

                    ushort value = ((ushort)((ushort)data[j] + ((ushort)data[j + 1] << 8))); // upper byte is unsigned.
                    c[i] = value;
                    if (value != 0)
                    {
                        nonZero = true;
                    }
                }

                // next 4 values are signed.
                for (int i = 4; i < 8; i++)
                {
                    int j = i * 2; // byte index.
                    short value = ((short)((short)data[j] + (short)data[j + 1] << 8)); // upper byte is unsigned.
                    c[i] = value;
                    if (value != 0)
                    {
                        nonZero = true;
                    }
                }

                if (nonZero)
                {
                    this.c = c;

                    if (Calibrated != null)
                    {
                        Calibrated(this, EventArgs.Empty);
                    }
                }
            }

            return this.c;
        }
        private void UpdateCalibrationData(GattValueChangedEventArgs eventArgs)
        {
            uint dataLength = eventArgs.CharacteristicValue.Length;
            using (DataReader reader = DataReader.FromBuffer(eventArgs.CharacteristicValue))
            {
                if (dataLength == 16)
                {
                    var data = new byte[dataLength];
                    reader.ReadBytes(data);

                    c = new int[8];
                    for (int i = 0; i < 8; i++)
                    {
                        int j = i * 2; // byte index.
                        c[i] = (int)data[j] + (data[j + 1] << 8); // upper byte is unsigned.
                    }

                    if (Calibrated != null)
                    {
                        Calibrated(this, EventArgs.Empty);
                    }
                }
            }
        }

    }


    public class BarometerMeasurement
    {
        /// <summary>
        /// Barometric pressure (hecto-pascal)
        /// </summary>
        public double HectoPascals { get; set;}

        public double Bars { get { return HectoPascals / 1000; } }

        public double MilliBars { get { return HectoPascals; } }

        public double Pascals { get { return HectoPascals * 100; } set { HectoPascals = value / 100; } }

        public double KiloPascals { get { return HectoPascals / 10; } }

        /// <summary>
        /// mm column of mercury
        /// </summary>
        public double HgMm { get { return HectoPascals * 0.750; } }

        /// <summary>
        /// inches of mercury
        /// </summary>
        public double HgInches { get { return HgMm / 25.4; } }

        /// <summary>
        /// Pounds per square inch
        /// </summary>
        public double Psi { get { return HectoPascals * 0.0145; } }


        public BarometerMeasurement()
        {
        }


    }

    public class BarometerMeasurementEventArgs : EventArgs
    {
        public BarometerMeasurementEventArgs(BarometerMeasurement measurement, DateTimeOffset timestamp)
        {
            Measurement = measurement;
            Timestamp = timestamp;
        }

        public BarometerMeasurement Measurement
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
