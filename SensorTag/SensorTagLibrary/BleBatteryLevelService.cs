using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace SensorTag
{
    public class BleBatteryLevelService : BleGenericGattService
    {

        public BleBatteryLevelService()
        {
        }

        public event EventHandler<BatteryLevelMeasurementEventArgs> BatteryLevelMeasurementValueChanged;

        public async Task<IEnumerable<BleGattDeviceInfo>> FindMatchingDevices()
        {
            var devices = await DeviceInformation.FindAllAsync(GattDeviceService.GetDeviceSelectorFromUuid(
                                                                 GattCharacteristicUuids.BatteryLevel), new string[] {
                                                                         CONTAINER_ID_PROPERTY_NAME
                                                                     });

            List<BleGattDeviceInfo> result = new List<BleGattDeviceInfo>();

            foreach (DeviceInformation device in devices)
            {
                result.Add(new BleGattDeviceInfo(device));
            }

            return result;
        }

        private void OnBatteryLevelMeasurementValueChanged(BatteryLevelMeasurementEventArgs args)
        {
            if (BatteryLevelMeasurementValueChanged != null)
            {
                BatteryLevelMeasurementValueChanged(this, args);
            }
        }

        public async Task<bool> ConnectAsync(string deviceContainerId)
        {
            bool rc = await this.ConnectAsync(GattServiceUuids.Battery, deviceContainerId);
            if (rc)
            {
                this.RegisterForValueChangeEvents(GattCharacteristicUuids.BatteryLevel);
            }
            return rc;
        }

        protected override void OnCharacteristicValueChanged(GattCharacteristic sender, GattValueChangedEventArgs eventArgs)
        {
            if (sender.Uuid == GattCharacteristicUuids.BatteryLevel)
            {
                if (BatteryLevelMeasurementValueChanged != null)
                {
                    byte level = 0;

                    using (DataReader reader = DataReader.FromBuffer(eventArgs.CharacteristicValue))
                    {
                        level = reader.ReadByte();
                    }
                    OnBatteryLevelMeasurementValueChanged(new BatteryLevelMeasurementEventArgs() { BatteryLevel = level, Timestamp = eventArgs.Timestamp });
                }
            }
        }

        public async Task<byte> ReadBatteryLevelAsync(BluetoothCacheMode cacheMode = BluetoothCacheMode.Uncached)
        {
            GattStatus gattStatus = GattStatus.Unsupported;
            
            var batteryLevelCharacteristic = this.GetCharacteristic(GattCharacteristicUuids.BatteryLevel);
            if (batteryLevelCharacteristic == null)
            {
                gattStatus = GattStatus.UnknownCharacteristic;
            }
            else
            {
                GattReadResult readResult = await batteryLevelCharacteristic.ReadValueAsync(cacheMode);
                gattStatus = (GattStatus)readResult.Status;
                if (gattStatus == GattStatus.Success)
                {
                    using (DataReader reader = DataReader.FromBuffer(readResult.Value))
                    {
                        return reader.ReadByte();
                    }
                }
            }

            OnError("ReadBatteryLevelAsync:" + gattStatus.ToString());
            return 0;
        }

    }


    public class BatteryLevelMeasurementEventArgs : EventArgs
    {

        public byte BatteryLevel
        {
            get;
            set;
        }

        public DateTimeOffset Timestamp
        {
            get;
            set;
        }
    }

}
