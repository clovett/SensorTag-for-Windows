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
    /// This class provides access to the SensorTag button (key) data.  
    /// </summary>
    public class BleButtonService : BleGenericGattService
    {

        public BleButtonService() 
        {
        }

        /// <summary>
        /// The version of the SensorTag device.  1=CC2541, 2=CC2650.
        /// </summary>
        public int Version { get; set; }


        public static Guid ButtonServiceUuid = Guid.Parse("0000ffe0-0000-1000-8000-00805f9b34fb");
        public static Guid ButtonCharacteristicUuid = Guid.Parse("0000ffe1-0000-1000-8000-00805f9b34fb");
        
        Delegate _buttonValueChanged;

        public event EventHandler<SensorButtonEventArgs> ButtonValueChanged
        {
            add
            {
                if (_buttonValueChanged != null)
                {
                    _buttonValueChanged = Delegate.Combine(_buttonValueChanged, value);
                }
                else
                {
                    _buttonValueChanged = value;
                    RegisterForValueChangeEvents(ButtonCharacteristicUuid);
                }
            }
            remove
            {
                if (_buttonValueChanged != null)
                {
                    _buttonValueChanged = Delegate.Remove(_buttonValueChanged, value);
                    if (_buttonValueChanged == null)
                    {
                        UnregisterForValueChangeEvents(ButtonCharacteristicUuid);
                    }
                }
            }
        }

        private void OnButtonValueChanged(SensorButtonEventArgs args)
        {
            if (_buttonValueChanged != null)
            {
                ((EventHandler<SensorButtonEventArgs>)_buttonValueChanged)(this, args);
            }
        }

        public async Task<bool> ConnectAsync(string deviceContainerId)
        {
            return await this.ConnectAsync(ButtonServiceUuid, deviceContainerId);
        }

        protected override void OnCharacteristicValueChanged(GattCharacteristic sender, GattValueChangedEventArgs eventArgs)
        {
            if (sender.Uuid == ButtonCharacteristicUuid)
            {
                if (_buttonValueChanged != null)
                {
                    uint dataLength = eventArgs.CharacteristicValue.Length;
                    using (DataReader reader = DataReader.FromBuffer(eventArgs.CharacteristicValue))
                    {
                        if (dataLength == 1)
                        {
                            byte bits = reader.ReadByte();

                            OnButtonValueChanged(new SensorButtonEventArgs(bits, eventArgs.Timestamp));
                        }
                    }
                }
            }
        }

    }


    public class SensorButtonEventArgs : EventArgs
    {
        private byte bits;

        public SensorButtonEventArgs(byte bits, DateTimeOffset timestamp)
        {
            this.bits = bits;
            Timestamp = timestamp;
        }

        public bool LeftButtonDown
        {
            get { return (bits & 0x2) == 0x2; }
        }

        public bool RightButtonDown
        {
            get { return (bits & 0x1) == 0x1; }
        }

        public DateTimeOffset Timestamp
        {
            get;
            private set;
        }
    }

}
