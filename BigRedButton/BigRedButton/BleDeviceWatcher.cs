using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;

namespace BigRedButton
{
    class BleDeviceWatcher
    {
        DeviceWatcher watcher;

        public void StartWatching(Guid gattServiceUuid)
        {
            var selector = GattDeviceService.GetDeviceSelectorFromUuid(gattServiceUuid);
            watcher = DeviceInformation.CreateWatcher(selector);
            watcher.Added += OnDeviceAdded;
            watcher.Removed += OnDeviceRemoved;
        }

        private void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            Debug.WriteLine("Device removed: {0}", args.Id);
        }

        private void OnDeviceAdded(DeviceWatcher sender, DeviceInformation args)
        {
            Debug.WriteLine("Device added: {0}: {1}", args.Name, args.Id);
        }
    }
}
