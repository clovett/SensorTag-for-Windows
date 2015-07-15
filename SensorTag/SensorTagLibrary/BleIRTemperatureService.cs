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
    public class BleIRTemperatureService : BleGenericGattService
    {

        public BleIRTemperatureService() 
        {
        }

        static Guid IRTemperatureServiceUuid = Guid.Parse("f000aa00-0451-4000-b000-000000000000");
        static Guid IRTemperatureCharacteristicUuid = Guid.Parse("f000aa01-0451-4000-b000-000000000000");
        static Guid IRTemperatureCharacteristicConfigUuid = Guid.Parse("f000aa02-0451-4000-b000-000000000000");
        static Guid IRTemperatureCharacteristicPeriodUuid = Guid.Parse("f000aa03-0451-4000-b000-000000000000");
        
        Delegate _irTemperatureValueChanged;

        public event EventHandler<IRTemperatureMeasurementEventArgs> IRTemperatureMeasurementValueChanged
        {
            add
            {
                if (_irTemperatureValueChanged != null)
                {
                    _irTemperatureValueChanged = Delegate.Combine(_irTemperatureValueChanged, value);
                }
                else
                {
                    _irTemperatureValueChanged = value;
                    RegisterForValueChangeEvents(IRTemperatureCharacteristicUuid);
                }
            }
            remove
            {
                if (_irTemperatureValueChanged != null)
                {
                    _irTemperatureValueChanged = Delegate.Remove(_irTemperatureValueChanged, value);
                }
                if (_irTemperatureValueChanged == null)
                {
                    UnregisterForValueChangeEvents(IRTemperatureCharacteristicUuid);
                }
            }
        }

#if FALSE

        // the documentation lies, the IR temp has no period characteristic

        /// <summary>
        /// Get the rate at which accelerometer is being polled, in milliseconds.  
        /// </summary>
        /// <returns>Returns the value read from the sensor or -1 if something goes wrong.</returns>
        public async Task<int> GetPeriod()
        {
            byte v = await ReadCharacteristicByte(IRTemperatureCharacteristicPeriodUuid, Windows.Devices.Bluetooth.BluetoothCacheMode.Uncached);
            return (int)(v * 10);
        }

        /// <summary>
        /// Set the rate at which accelerometer is being polled, in milliseconds.  
        /// </summary>
        /// <param name="milliseconds">The delay between updates, accurate only to 10ms intervals. </param>
        public async void SetPeriod(int milliseconds)
        {
            int delay = milliseconds / 10;
            if (delay < 0)
            {
                delay = 1;
            }

            await WriteCharacteristicByte(IRTemperatureCharacteristicPeriodUuid, (byte)delay);
        }
#endif 

        public async Task StartReading()
        {
            await WriteCharacteristicByte(IRTemperatureCharacteristicConfigUuid, 1);
        }

        public async Task StopReading()
        {
            await WriteCharacteristicByte(IRTemperatureCharacteristicConfigUuid, 0);
        }

        private void OnIRTemperatureMeasurementValueChanged(IRTemperatureMeasurementEventArgs args)
        {
            if (_irTemperatureValueChanged != null)
            {
                ((EventHandler<IRTemperatureMeasurementEventArgs>)_irTemperatureValueChanged)(this, args);
            }
        }


        public async Task<bool> ConnectAsync()
        {
            return await this.ConnectAsync(IRTemperatureServiceUuid, null);
        }

        protected override void OnCharacteristicValueChanged(GattCharacteristic sender, GattValueChangedEventArgs eventArgs)
        {
            if (sender.Uuid == IRTemperatureCharacteristicUuid)
            {
                if (_irTemperatureValueChanged != null)
                {
                    uint dataLength = eventArgs.CharacteristicValue.Length;
                    using (DataReader reader = DataReader.FromBuffer(eventArgs.CharacteristicValue))
                    {
                        if (dataLength == 4)
                        {
                            var data = new byte[dataLength];
                            reader.ReadBytes(data);

                            IRTemperatureMeasurement measurement = new IRTemperatureMeasurement();
                            short objTemp = (short)(data[0] + (data[1] << 8));
                            ushort dieTemp = (ushort)(data[2] + (data[3] << 8));

                            measurement.DieTemperature = dieTemp / 128.0;

                            measurement.SetTargetTemperature(objTemp);                            

                            OnIRTemperatureMeasurementValueChanged(new IRTemperatureMeasurementEventArgs(measurement, eventArgs.Timestamp));
                        }
                    }
                }
            }
        }

    }


    public class IRTemperatureMeasurement
    {
        public double ObjectTemperature
        {
            get;
            set;
        }

        /// <summary>
        /// Temperature measured on the die where sensor is mounted.
        /// </summary>
        public double DieTemperature
        {
            get;
            set;
        }

        public IRTemperatureMeasurement()
        {
        }


        internal void SetTargetTemperature(short twoByteValue)
        {
            double Vobj2 = (double)twoByteValue;

            Vobj2 *= 0.00000015625;

            double Tdie = DieTemperature + 273.15;

            double S0 = 7E-14;	// Calibration factor: todo: this factor needs to be computed via calibration step.
            double a1 = 1.75E-3;
            double a2 = -1.678E-5;
            double b0 = -2.94E-5;
            double b1 = -5.7E-7;
            double b2 = 4.63E-9;
            double c2 = 13.4;
            double Tref = 298.15;
            double S = S0 * (1 + a1 * (Tdie - Tref) + a2 * Math.Pow((Tdie - Tref), 2));
            double Vos = b0 + b1 * (Tdie - Tref) + b2 * Math.Pow((Tdie - Tref), 2);
            double fObj = (Vobj2 - Vos) + c2 * Math.Pow((Vobj2 - Vos), 2);
            double tObj = Math.Pow(Math.Pow(Tdie, 4) + (fObj / S), .25);

            ObjectTemperature = (tObj - 273.15);
        }
    }

    public class IRTemperatureMeasurementEventArgs : EventArgs
    {
        public IRTemperatureMeasurementEventArgs(IRTemperatureMeasurement measurement, DateTimeOffset timestamp)
        {
            Measurement = measurement;
            Timestamp = timestamp;
        }

        public IRTemperatureMeasurement Measurement
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
