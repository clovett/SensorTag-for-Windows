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

        /// <summary>
        /// The version of the SensorTag device.  1=CC2541, 2=CC2650.
        /// </summary>
        public int Version { get; set; }

        static Guid BarometerServiceUuid = Guid.Parse("f000aa40-0451-4000-b000-000000000000");
        static Guid BarometerCharacteristicUuid = Guid.Parse("f000aa41-0451-4000-b000-000000000000");
        static Guid BarometerCharacteristicConfigUuid = Guid.Parse("f000aa42-0451-4000-b000-000000000000");
        // Only used on CC2541.
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
                    RegisterForValueChangeEvents(BarometerCharacteristicUuid);
                }
            }
            remove
            {
                if (_barometerValueChanged != null)
                {
                    _barometerValueChanged = Delegate.Remove(_barometerValueChanged, value);
                    if (_barometerValueChanged == null)
                    {
                        UnregisterForValueChangeEvents(BarometerCharacteristicUuid);
                    }
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

        bool isReading;

        public async Task StartReading()
        {
            if (!isReading)
            {
                await WriteCharacteristicByte(BarometerCharacteristicConfigUuid, 1);
                isReading = true;
            }
        }

        public async Task StopReading()
        {
            if (isReading)
            {
                isReading = false;
                await WriteCharacteristicByte(BarometerCharacteristicConfigUuid, 0);
            }
        }


        public async Task StartCalibration()
        {
            if (Version == 1)
            {
                await WriteCharacteristicByte(BarometerCharacteristicConfigUuid, 2);
                await ReadCalibration();
            }
            else
            {
                // Calibration is done in firmware on the CC2650 firmware.
                if (Calibrated != null)
                {
                    Calibrated(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler Calibrated;

        /// <summary>
        /// Get the rate at which sensor is being polled, in milliseconds.  
        /// </summary>
        /// <returns>Returns the value read from the sensor or -1 if something goes wrong.</returns>
        public async Task<int> GetPeriod()
        {
            if (Version > 1)
            {
                byte v = await ReadCharacteristicByte(BarometerCharacteristicPeriodUuid, Windows.Devices.Bluetooth.BluetoothCacheMode.Uncached);
                return (int)(v * 10);
            }
            return 1000;
        }

        /// <summary>
        /// Set the rate at which sensor is being polled.
        /// The period ranges for 100 ms to 2.55 seconds, resolution 10 ms.
        /// </summary>
        /// <param name="milliseconds">The delay between updates, accurate only to 10ms intervals. </param>
        public async Task SetPeriod(int milliseconds)
        {
            if (Version > 1)
            {
                int delay = milliseconds / 10;
                if (delay < 0)
                {
                    delay = 1;
                }
                await WriteCharacteristicByte(BarometerCharacteristicPeriodUuid, (byte)delay);
            }
        }

        private void OnBarometerMeasurementValueChanged(BarometerMeasurementEventArgs args)
        {
            if (_barometerValueChanged != null)
            {
                ((EventHandler<BarometerMeasurementEventArgs>)_barometerValueChanged)(this, args);
            }
        }

        public async Task<bool> ConnectAsync(string deviceContainerId)
        {
            return await this.ConnectAsync(BarometerServiceUuid, deviceContainerId);
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
                            // version 1
                            var data = new byte[dataLength];
                            reader.ReadBytes(data);
                            CalcBarometricPressure(eventArgs.Timestamp, data);
                        }
                        else if (dataLength == 6)
                        {
                            // version 2
                            uint temp = ReadBigEndianU24bit(reader);
                            uint pressure = ReadBigEndianU24bit(reader);
                            BarometerMeasurement measurement = new BarometerMeasurement();
                            measurement.Temperature = (double)temp / 100.0;
                            measurement.HectoPascals = (double)pressure / 100.0;
                            OnBarometerMeasurementValueChanged(new BarometerMeasurementEventArgs(measurement, eventArgs.Timestamp));
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

            if (this.c != null)
            {
                BarometerMeasurement measurement = new BarometerMeasurement();

                //more info about the calculation:
                //http://www.epcos.com/web/generator/Web/Sections/ProductCatalog/Sensors/PressureSensors/T5400-ApplicationNote,property=Data__en.pdf;/T5400_ApplicationNote.pdf                

                int tr = BitConverter.ToInt16(data, 0); // Temperature raw value
                int pr = BitConverter.ToUInt16(data, 2); // Pressure raw value from sensor

                // Temperature actual value in unit centi degrees celsius
                double t_a = (100 * (c[0] * tr / Math.Pow(2, 8) + c[1] * Math.Pow(2, 6))) / Math.Pow(2, 16);
                double sensitivity = c[2] + c[3] * tr / Math.Pow(2, 17) + ((c[4] * tr / Math.Pow(2, 15)) * tr) / Math.Pow(2, 19);
                double offset = c[5] * Math.Pow(2, 14) + c[6] * tr / Math.Pow(2, 3) + ((c[7] * tr / Math.Pow(2, 15)) * tr) / Math.Pow(2, 4);

                measurement.Pascals = (sensitivity * pr + offset) / Math.Pow(2, 14);

                OnBarometerMeasurementValueChanged(new BarometerMeasurementEventArgs(measurement, timestamp));
            }
        }


        private async Task<int[]> ReadCalibration()
        {
            byte[] data = await ReadCharacteristicBytes(BarometerCharacteristicCalibrationUuid, Windows.Devices.Bluetooth.BluetoothCacheMode.Uncached);
            if (data.Length == 16)
            {
                SetCalibration(data);
            }

            return this.c;
        }

        private void SetCalibration(byte[] data)
        {
            int[] calibrationData = new int[8];

            calibrationData[0] = BitConverter.ToUInt16(data, 0);
            calibrationData[1] = BitConverter.ToUInt16(data, 2);
            calibrationData[2] = BitConverter.ToUInt16(data, 4);
            calibrationData[3] = BitConverter.ToUInt16(data, 6);
            calibrationData[4] = BitConverter.ToInt16(data, 8);
            calibrationData[5] = BitConverter.ToInt16(data, 10);
            calibrationData[6] = BitConverter.ToInt16(data, 12);
            calibrationData[7] = BitConverter.ToInt16(data, 14);


            this.c = calibrationData;

            if (Calibrated != null)
            {
                Calibrated(this, EventArgs.Empty);
            }
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
                    SetCalibration(data);
                }
            }
        }

    }

    public enum PressureUnit
    {
        Hectopascal,
        Pascal,
        Bar,
        Millibar,
        Kilopascal,
        MercuryMm,
        MercuryIn,
        Psi
    }


    public class BarometerMeasurement
    {
        /// <summary>
        /// Temperature reading that comes from Bosch Sensortec BMP280
        /// </summary>
        public double Temperature { get; set; }


        /// <summary>
        /// Barometric pressure (hecto-pascal)
        /// </summary>
        public double HectoPascals { get; set; }

        public double GetUnit(PressureUnit unit)
        {
            switch (unit)
            {
                case PressureUnit.Hectopascal:
                    return this.HectoPascals;
                case PressureUnit.Pascal:
                    return this.Pascals;
                case PressureUnit.Bar:
                    return this.Bars;
                case PressureUnit.Millibar:
                    return this.MilliBars;
                case PressureUnit.Kilopascal:
                    return this.KiloPascals;
                case PressureUnit.MercuryMm:
                    return this.HgMm;
                case PressureUnit.MercuryIn:
                    return this.HgInches;
                case PressureUnit.Psi:
                    return this.Psi;
            }

            return this.HectoPascals;
        }

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
