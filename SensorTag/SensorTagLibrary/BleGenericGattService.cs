using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Devices.Enumeration.Pnp;
using Windows.Storage.Streams;

namespace Microsoft.MobileLabs.Bluetooth
{
    public enum GattStatus
    {
        Success = GattCommunicationStatus.Success,
        Unreacheable = GattCommunicationStatus.Unreachable,
        Unsupported,
        UnknownCharacteristic
    }

    public class GattStatusException : Exception
    {
        public GattStatusException(GattStatus gattStatus)
        {
            Status = gattStatus;
        }

        public GattStatus Status
        {
            get;
            private set;
        }
    }

    public class BleGenericGattService : IDisposable
    {
        GattDeviceService _service;
        PnpObjectWatcher _connectionWatcher;
        List<GattCharacteristic> _characteristics = new List<GattCharacteristic>();              
        Guid _requestedServiceGuid;
        List<Guid> _requestedCharacteristics = new List<Guid>();
        bool _connected;
        CancellationTokenSource reconnectTaskCancel;

        protected virtual void Dispose(bool disposing)
        {
            UnregisterForConnectionEvents();
            Disconnect();
        }

        public bool IsConnected { get { return _connected; } }

        private async void Disconnect()
        {
            foreach (var characteristic in _characteristics)
            {
                await UnregisterCharacteristic(characteristic);
            }
            _characteristics.Clear();
            if (_service != null)
            {
                // bugbug: if we call _service.Dispose below then the app will get System.ObjectDisposedException'
                // next time it tries to reconnect to the device because the Windows BLE stack gives us back the 
                // disposed object next time we call GattDeviceService.FromIdAsync and there is no workaround
                // besides this.  The downside to this is that the phone will not "disconnect" from the BLE
                // device.  Not even after the app is closed.
                //try
                //{
                //    _service.Dispose();
                //}
                //catch { }

                Debug.WriteLine("_service disconnected: " + this.GetType().Name);
                _service = null;
            }
            if (reconnectTaskCancel != null)
            {
                reconnectTaskCancel.Cancel();
                reconnectTaskCancel = null;
            }
            _connected = false;
        }

        protected virtual void OnCharacteristicValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
        }


        public event EventHandler<ConnectionChangedEventArgs> ConnectionChanged;

        protected void OnConnectionChanged(bool connected)
        {
            this._connected = connected;

            if (ConnectionChanged != null)
            {
                ConnectionChanged(this, new ConnectionChangedEventArgs(connected));
            }
        }

        public event EventHandler<string> Error;

        protected void OnError(string message)
        {
            if (Error != null)
            {
                Error(this, message);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~BleGenericGattService()
        {
            Dispose(false);
        }

        public string DeviceName { get; set; }

        public string DeviceContainerId { get; set; }

        private static readonly string CONTAINER_ID_PROPERTY_NAME = "System.Devices.ContainerId";
        private static readonly string CONNECTED_FLAG_PROPERTY_NAME = "System.Devices.Connected";

        private Dictionary<String, String> deviceId2Name = new Dictionary<string, string>();
        protected async Task<Dictionary<String,String>> FindMatchingDevices(Guid serviceGuid)
        {
            var devices = await DeviceInformation.FindAllAsync(GattDeviceService.GetDeviceSelectorFromUuid(
                                                                 serviceGuid), new string[] {
                                                                         CONTAINER_ID_PROPERTY_NAME
                                                                     });
            this.deviceId2Name.Clear();
            foreach( DeviceInformation device in devices)
            {
                string id = device.Properties[CONTAINER_ID_PROPERTY_NAME].ToString();
                this.deviceId2Name[id] = device.Name;
            }

            return this.deviceId2Name;
        }
        /// <summary>
        /// Initialize the new service
        /// </summary>
        /// <param name="serviceGuid">One of GattServiceUuids</param>
        /// <param name="deviceContainerId">The device you are interested in or null if you want any device </param>
        /// <param name="characteristics">One or more GattCharacteristicUuids</param>
        /// <returns></returns>
        protected async Task<bool> ConnectAsync(Guid serviceGuid, string deviceContainerId)
        {
            this._requestedServiceGuid = serviceGuid;

            var devices = await DeviceInformation.FindAllAsync(GattDeviceService.GetDeviceSelectorFromUuid(
                                                                     serviceGuid), new string[] {
                                                                         CONTAINER_ID_PROPERTY_NAME
                                                                     });

            if (devices.Count == 0)
            {
                _connected = false;
                OnError("no devices found");
                return false;
            }
            
            DeviceInformation deviceInformation = null;

            foreach (DeviceInformation device in devices)
            {
                string id = device.Properties[CONTAINER_ID_PROPERTY_NAME].ToString();
                if (deviceContainerId == null || string.Compare(id, deviceContainerId, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    DeviceContainerId = id;
                    deviceInformation = device;
                    break;
                }
            }

            if (deviceInformation == null)
            {
                _connected = false;
                OnError("requested device not found");
                return false;
            }

            DeviceName = deviceInformation.Name;

            _service = await GattDeviceService.FromIdAsync(deviceInformation.Id);
            if (_service == null)
            {
                _connected = false;
                throw new Exception("Service not available, is another app still running that is using the pinweight service?");
            }

            _connected = true;

            // don't wait on this, async is fine.
            var nowait = RegisterForConnectionEvents();

            OnConnectionChanged(_connected);

            if (_service != null) 
            {
                Debug.WriteLine("Service connected: " + this.GetType().Name);
            }
            return true;
        }

        public async Task RegisterForValueChangeEvents(Guid guid)
        {
            if (_service == null)
            {
                Debug.WriteLine("RegisterForValueChangeEvents called when service was disconnected");
                return;
            }
            GattCharacteristic characteristic = _service.GetCharacteristics(guid).FirstOrDefault();

            if (characteristic == null)
            {
                OnError("Characteristic " + guid + " not available");
                return;
            }

            GattCharacteristicProperties properties = characteristic.CharacteristicProperties;

            Debug.WriteLine("Characteristic {0} supports: ", guid.ToString("b"));
            if ((properties & GattCharacteristicProperties.Broadcast) != 0)
            {
                Debug.WriteLine("    Broadcast");
            }
            if ((properties & GattCharacteristicProperties.Read) != 0)
            {
                Debug.WriteLine("    Read");
            }
            if ((properties & GattCharacteristicProperties.WriteWithoutResponse) != 0)
            {
                Debug.WriteLine("    WriteWithoutResponse");
            }
            if ((properties & GattCharacteristicProperties.Write) != 0)
            {
                Debug.WriteLine("    Write");
            }
            if ((properties & GattCharacteristicProperties.Notify) != 0)
            {
                Debug.WriteLine("    Notify");
            }
            if ((properties & GattCharacteristicProperties.Indicate) != 0)
            {
                Debug.WriteLine("    Indicate");
            }
            if ((properties & GattCharacteristicProperties.AuthenticatedSignedWrites) != 0)
            {
                Debug.WriteLine("    AuthenticatedSignedWrites");
            }
            if ((properties & GattCharacteristicProperties.ExtendedProperties) != 0)
            {
                Debug.WriteLine("    ExtendedProperties");
            }
            if ((properties & GattCharacteristicProperties.ReliableWrites) != 0)
            {
                Debug.WriteLine("    ReliableWrites");
            }
            if ((properties & GattCharacteristicProperties.WritableAuxiliaries) != 0)
            {
                Debug.WriteLine("    WritableAuxiliaries");
            }

            if ((properties & GattCharacteristicProperties.Notify) != 0)
            {
                characteristic.ValueChanged += OnCharacteristicValueChanged;

                GattCommunicationStatus status = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.Notify);

                if (status != GattCommunicationStatus.Success)
                {
                    Debug.WriteLine("GattClientCharacteristicConfigurationDescriptorValue.Notify: " + status);
                    if (status == GattCommunicationStatus.Unreachable)
                    {
                        throw new Exception("Registering to get notification from the device failed saying device is unreachable.  Perhaps the device is connected to another computer?");
                    }
                }
            }

            this._requestedCharacteristics.Add(guid);
            _characteristics.Add(characteristic);
        }

        public async Task UnregisterForValueChangeEvents(Guid guid)
        {
            foreach (var characteristic in _characteristics.ToArray())
            {
                if (characteristic.Uuid == guid)
                {
                    await UnregisterCharacteristic(characteristic);
                }
            }
            this._requestedCharacteristics.Remove(guid);
        }

        private async Task UnregisterCharacteristic(GattCharacteristic characteristic)
        {
            GattCharacteristicProperties properties = characteristic.CharacteristicProperties;
            if ((properties & GattCharacteristicProperties.Notify) != 0)
            {
                // stop notifying.
                GattCommunicationStatus status = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.None);

                characteristic.ValueChanged -= OnCharacteristicValueChanged;
            }
        }

        public GattCharacteristic GetCharacteristic(Guid guid)
        {
            if (_service == null)
            {
                return null;
            }
            foreach (var ch in _characteristics)
            {
                if (ch.Uuid == guid)
                {
                    return ch;
                }
            }

            return _service.GetCharacteristics(guid).FirstOrDefault();
        }

        async Task<bool> RegisterForConnectionEvents()
        {
            UnregisterForConnectionEvents();

            string deviceContainerId = "{" + DeviceContainerId + "}";
            PnpObject containerPnpObject = await PnpObject.CreateFromIdAsync(PnpObjectType.DeviceContainer, deviceContainerId, new string[] { CONNECTED_FLAG_PROPERTY_NAME });

            var connectedProperty = containerPnpObject.Properties[CONNECTED_FLAG_PROPERTY_NAME];
            bool isConnected = false;
            Boolean.TryParse(connectedProperty.ToString(), out isConnected);
            _connectionWatcher = PnpObject.CreateWatcher(PnpObjectType.DeviceContainer,
               new string[] { CONNECTED_FLAG_PROPERTY_NAME }, String.Empty);

            _connectionWatcher.Updated += DeviceConnection_Updated;
            _connectionWatcher.Start();

            return isConnected;
        }

        private void DeviceConnection_Updated(PnpObjectWatcher sender, PnpObjectUpdate args)
        {
            var connectedProperty = args.Properties[CONNECTED_FLAG_PROPERTY_NAME];
            bool isConnected = false;
            string deviceContainerId = "{" + DeviceContainerId + "}";

            Debug.WriteLine("DeviceConnection_Updated: " + connectedProperty.ToString());

            if ((deviceContainerId == args.Id) && Boolean.TryParse(connectedProperty.ToString(), out isConnected))
            {
                if (!_connected && isConnected)
                {
                    if (reconnectTaskCancel != null && !reconnectTaskCancel.IsCancellationRequested)
                    {
                        reconnectTaskCancel.Cancel();
                    }
                    reconnectTaskCancel = new CancellationTokenSource();
                    var nowait = Task.Run(new Action(() => { ReregisterAllValueChangeEvents(); }), reconnectTaskCancel.Token);
                }             
                OnConnectionChanged(isConnected);
            }
        }

        private async void ReregisterAllValueChangeEvents()
        {
            var token = reconnectTaskCancel.Token;
            List<Guid> temp = this._requestedCharacteristics;
            _requestedCharacteristics = new List<Guid>();
            _characteristics = new List<GattCharacteristic>();
            while (!token.IsCancellationRequested && temp.Count > 0)
            {
                foreach (Guid guid in temp.ToArray())
                {
                    try
                    {
                        await RegisterForValueChangeEvents(guid);
                        temp.Remove(guid); // this one is good.
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Error registering value change event: " + ex.Message);
                    }

                    // sometimes we get a weird exception saying some semaphore has timed out inside
                    // the BLE stack with HRESULT 0x80070079, so if that happens we wait a bit and try again      
                    if (!token.IsCancellationRequested)
                    {
                        await Task.Delay(500, token);
                    }
                }
            }
        }

        public void UnregisterForConnectionEvents()
        {
            if (_connectionWatcher != null)
            {
                _connectionWatcher.Updated -= DeviceConnection_Updated;
                _connectionWatcher.Stop();
                _connectionWatcher = null;
            }
        }

        public async Task WriteCharacteristicByte(Guid characteristicGuid, byte value)
        {
            var ch = GetCharacteristic(characteristicGuid);
            if (ch != null)
            {
                var properties = ch.CharacteristicProperties;

                if ((properties & GattCharacteristicProperties.Write) != 0)
                {
                    DataWriter writer = new DataWriter();
                    writer.WriteByte(value);
                    var buffer = writer.DetachBuffer();
                    var status = await ch.WriteValueAsync(buffer, GattWriteOption.WriteWithResponse);
                    if (status != GattCommunicationStatus.Success)
                    {
                        throw new Exception("Write failed: " + status.ToString());
                    }
                }
                else
                {
                    throw new Exception(string.Format("Characteristic '{0}' does not support GattCharacteristicProperties.Write"));
                }
            }
            else
            {
                throw new Exception(string.Format("Characteristic '{0}' not found", characteristicGuid.ToString()));
            }
        }
        
        public async Task<byte> ReadCharacteristicByte(Guid characteristicGuid, BluetoothCacheMode cacheMode)
        {
            var ch = GetCharacteristic(characteristicGuid);
            if (ch != null)
            {
                var properties = ch.CharacteristicProperties;

                if ((properties & GattCharacteristicProperties.Read) != 0)
                {
                    var result = await ch.ReadValueAsync(cacheMode);
                    var status = result.Status;
                    if (status != GattCommunicationStatus.Success)
                    {
                        throw new Exception("Read failed: " + status.ToString());                        
                    }
                    IBuffer buffer = result.Value;
                    DataReader reader = DataReader.FromBuffer(buffer);
                    var value = reader.ReadByte();
                    return value;
                }
                else
                {
                    throw new Exception(string.Format("Characteristic '{0}' does not support GattCharacteristicProperties.Read"));
                }
            }
            else
            {
                throw new Exception(string.Format("Characteristic '{0}' not found", characteristicGuid.ToString()));
            }
        }
        public async Task<byte[]> ReadCharacteristicBytes(Guid characteristicGuid, BluetoothCacheMode cacheMode)
        {
            var ch = GetCharacteristic(characteristicGuid);
            if (ch != null)
            {
                var properties = ch.CharacteristicProperties;

                if ((properties & GattCharacteristicProperties.Read) != 0)
                {
                    var result = await ch.ReadValueAsync(cacheMode);
                    var status = result.Status;
                    if (status != GattCommunicationStatus.Success)
                    {
                        throw new Exception("Read failed: " + status.ToString());
                    }
                    IBuffer buffer = result.Value;
                    uint size = buffer.Length;
                    DataReader reader = DataReader.FromBuffer(buffer);
                    byte[] data = new byte[size];
                    reader.ReadBytes(data);
                    return data;
                }
                else
                {
                    throw new Exception(string.Format("Characteristic '{0}' does not support GattCharacteristicProperties.Read"));
                }
            }
            else
            {
                throw new Exception(string.Format("Characteristic '{0}' not found", characteristicGuid.ToString()));
            }
        }
    }


    public class ConnectionChangedEventArgs : EventArgs
    {
        public ConnectionChangedEventArgs(bool connected)
        {
            IsConnected = connected;
        }

        public bool IsConnected
        {
            get;
            private set;
        }
    }

}
