using System;
using System.Collections.Concurrent;
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

namespace SensorTag
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
        HashSet<Guid> _requestedCharacteristics = new HashSet<Guid>();
        bool _connected;
        bool _disconnecting;
        BleGattDeviceInfo _connectedDevice;

        protected virtual void Dispose(bool disposing)
        {
            UnregisterForConnectionEvents();
            Disconnect();
        }

        public bool IsConnected { get { return _connected; } }

        public bool IsDisconnecting
        {
            get { return _disconnecting; }
        }

        /// <summary>
        /// Get address of connected device or zero if no device is connected.
        /// </summary>
        public ulong MacAddress
        {
            get { return _connectedDevice != null ? _connectedDevice.Address : 0; }
        }


        private async void Disconnect()
        {
            if (_disconnecting)
            {
                return;
            }
            _connectedDevice = null;
            _disconnecting = true;
            await this.UnregisterAllValueChangeEvents();

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
            _disconnecting = false;
            _connected = false; 

            if (DisconnectFinished != null)
            {
                DisconnectFinished(this, EventArgs.Empty);
            }

        }

        protected virtual void OnCharacteristicValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            // handled by subclasses
        }

        public event EventHandler DisconnectFinished;

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

        internal static readonly string CONTAINER_ID_PROPERTY_NAME = "System.Devices.ContainerId";
        private static readonly string CONNECTED_FLAG_PROPERTY_NAME = "System.Devices.Connected";

        public static async Task<IEnumerable<BleGattDeviceInfo>> FindMatchingDevices(Guid serviceGuid)
        {
            var devices = await DeviceInformation.FindAllAsync(GattDeviceService.GetDeviceSelectorFromUuid(
                                                                 serviceGuid), new string[] {
                                                                         CONTAINER_ID_PROPERTY_NAME
                                                                     });

            List<BleGattDeviceInfo> result = new List<BleGattDeviceInfo>();

            foreach (DeviceInformation device in devices)
            {
                result.Add(new BleGattDeviceInfo(device));
            }

            return result;
        }

        /// <summary>
        /// Initialize the new service
        /// </summary>
        /// <param name="serviceGuid">One of GattServiceUuids</param>
        /// <param name="deviceContainerId">The device you are interested in or null if you want any device </param>
        /// <param name="radioAddress">Optional radio address to match</param>
        /// <returns></returns>
        protected async Task<bool> ConnectAsync(Guid serviceGuid, string deviceContainerId, long radioAddress = -1)
        {
            _disconnecting = false;

            this._requestedServiceGuid = serviceGuid;

            _connectedDevice = null;

            var devices = await FindMatchingDevices(serviceGuid);

            if (!devices.Any())
            {
                _connected = false;
                OnError("no devices found, try using bluetooth settings to pair your device");
                return false;
            }

            BleGattDeviceInfo matchingDevice = null;

            foreach (var device in devices)
            {
                string id = device.ContainerId;
                if (deviceContainerId == null || string.Compare(id, deviceContainerId, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    if (radioAddress == -1 || device.Address == (ulong)radioAddress)
                    {
                        DeviceContainerId = id;
                        matchingDevice = device;
                        break;
                    }
                }
            }

            if (matchingDevice == null)
            {
                _connected = false;
                OnError("requested device not found");
                return false;
            }

            DeviceName = matchingDevice.DeviceInformation.Name;

            _service = await GattDeviceService.FromIdAsync(matchingDevice.DeviceInformation.Id);
            if (_service == null)
            {
                _connected = false;
                throw new Exception("Service not available, is another app still running that is using the service?");
            }

            _connected = true;
            _connectedDevice = matchingDevice;

            // don't wait on this, async is fine.
            var nowait = RegisterForConnectionEvents();

            OnConnectionChanged(_connected);

            // in case the event handlers were added before Connect().
            ReregisterAllValueChangeEvents();

            if (_service != null)
            {
                Debug.WriteLine("Service connected: " + this.GetType().Name);
            }
            return true;
        }

        ConcurrentQueue<GattCharacteristic> registerNotifyQueue = new ConcurrentQueue<GattCharacteristic>();
        bool enableNotifyThreadRunning;

        private void QueueAsyncEnableNotification(GattCharacteristic characteristic)
        {
            registerNotifyQueue.Enqueue(characteristic);
            if (!enableNotifyThreadRunning && _service != null)
            {
                enableNotifyThreadRunning = true;
                Task.Run(new Action(ProcessEnableNotificationQueue));
            }

        }

        private void ProcessEnableNotificationQueue()
        {
            try
            {
                int retry = 5;
                GattCharacteristic characteristic;
                while (retry > 0)
                {
                    if (registerNotifyQueue.TryDequeue(out characteristic))
                    {
                        //characteristic.ProtectionLevel = GattProtectionLevel.Plain;

                        var task = characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify).AsTask();
                        task.Wait();

                        GattCommunicationStatus status = task.Result;
                        Debug.WriteLine("GattClientCharacteristicConfigurationDescriptorValue.Notify: " + status + " " + characteristic.Uuid.ToString("b"));

                        if (status != GattCommunicationStatus.Success)
                        {
                            if (status == GattCommunicationStatus.Unreachable)
                            {
                                OnError("Registering to get notification from the device failed saying device is unreachable.  Perhaps the device is connected to another computer?");
                            }
                        }
                        else
                        {
                            characteristic.ValueChanged += OnCharacteristicValueChanged;

                            // this characteristic should now be notifying.
                            lock (_characteristics)
                            {
                                _characteristics.Add(characteristic);
                            }
                        }
                    }
                    else if (retry-- > 0)
                    {
                        Task.Delay(100);
                    }
                }
            }
            catch (Exception ex)
            {
                OnError("Registering to get notification from the device failed.  " + ex.Message);
            }
            enableNotifyThreadRunning = false; 

        }

        public bool CanNotify(Guid characteristicGuid)
        {

            try
            {
                GattCharacteristic characteristic = _service.GetCharacteristics(characteristicGuid).FirstOrDefault();
                return (characteristic != null && characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify));
            }
            catch
            {
            }
            return false;
        }

        protected void RegisterForValueChangeEvents(Guid guid)
        {
            _requestedCharacteristics.Add(guid);

            if (_service == null || !this._connected)
            {
                // wait till we are connected...
                return;
            }
            try
            {
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
                    try {
                        characteristic.ValueChanged -= OnCharacteristicValueChanged;
                    } catch { }

                    QueueAsyncEnableNotification(characteristic);

                }
                else
                {
                    OnError("Registering for notification on characteristic that doesn't support notification: " + guid);
                }
            }
            catch (Exception ex)
            {
                OnError("Unhandled exception registering for notifications. " + ex.Message);
            }
        }

        GattCharacteristic[] GetSafeCharacteristics()
        {
            GattCharacteristic[] temp;
            if (_characteristics == null)
            {
                return new GattCharacteristic[0];
            }
            lock (_characteristics)
            {
                temp = _characteristics.ToArray();
            }
            return temp;
        }


        public async void UnregisterForValueChangeEvents(Guid guid)
        {
            foreach (var characteristic in GetSafeCharacteristics())
            {
                if (characteristic.Uuid == guid)
                {
                    await UnregisterCharacteristic(characteristic);
                    break;
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

                if (status == GattCommunicationStatus.Unreachable)
                {
                    OnError("Unregistering notification from the device failed saying device is unreachable.");
                }

                characteristic.ValueChanged -= OnCharacteristicValueChanged;
            }
        }

        public GattCharacteristic GetCharacteristic(Guid guid)
        {
            if (_service == null)
            {
                return null;
            }
            foreach (var ch in GetSafeCharacteristics())
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

            _connectionWatcher.Updated -= DeviceConnection_Updated;
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
                    var nowait = Task.Run(new Action(() => { ReregisterAllValueChangeEvents(); }));
                }
                OnConnectionChanged(isConnected);
            }
        }

        private async Task UnregisterAllValueChangeEvents()
        {
            foreach (var characteristic in GetSafeCharacteristics())
            {
                await UnregisterCharacteristic(characteristic);
            }
            lock (_characteristics)
            {
                _characteristics.Clear();
            }
        }

        private void ReregisterAllValueChangeEvents()
        {
            List<Guid> temp = new List<Guid>(this._requestedCharacteristics);
            lock (_characteristics)
            {
                _characteristics.Clear();
            }
            foreach (Guid guid in temp)
            { 
                RegisterForValueChangeEvents(guid);
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


        public async Task WriteCharacteristicBytes(Guid characteristicGuid, IBuffer buffer)
        {
            var ch = GetCharacteristic(characteristicGuid);
            if (ch != null)
            {
                var properties = ch.CharacteristicProperties;

                if ((properties & GattCharacteristicProperties.Write) != 0)
                {
                    var status = await ch.WriteValueAsync(buffer, GattWriteOption.WriteWithResponse);
                    if (status != GattCommunicationStatus.Success)
                    {
                        throw new Exception("Write failed: " + status.ToString());
                    }
                }
                else if ((properties & GattCharacteristicProperties.WriteWithoutResponse) != 0)
                {
                    var status = await ch.WriteValueAsync(buffer, GattWriteOption.WriteWithoutResponse);
                    if (status != GattCommunicationStatus.Success)
                    {
                        throw new Exception("Write failed: " + status.ToString());
                    }
                }
                else
                {
                    throw new Exception(string.Format("Characteristic '{0}' does not support GattCharacteristicProperties.Write or WriteWithoutResponse", characteristicGuid));
                }
            }
            else
            {
                throw new Exception(string.Format("Characteristic '{0}' not found", characteristicGuid.ToString()));
            }
        }


        public async Task WriteCharacteristicBytes(Guid characteristicGuid, byte[] value)
        {
            DataWriter writer = new DataWriter();
            writer.WriteBytes(value);
            var buffer = writer.DetachBuffer();
            await WriteCharacteristicBytes(characteristicGuid, buffer);
        }

        public async Task WriteCharacteristicByte(Guid characteristicGuid, byte value)
        {
            await WriteCharacteristicBytes(characteristicGuid, new byte[] { value });
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

        public async Task<IBuffer> ReadCharacteristicBuffer(Guid characteristicGuid, BluetoothCacheMode cacheMode)
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
                    return result.Value;
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

        protected ushort BigEndianUInt16(byte lo, byte hi)
        {
            return (ushort)(((ushort)hi << 8) + (ushort)lo);
        }

        protected void WriteBigEndianUInt16(ushort value, byte[] buffer, int index)
        {
            byte lo = (byte)(value & 0xff);
            byte hi = (byte)(value >> 8);
            buffer[index] = lo;
            buffer[index + 1] = hi;
        }

        protected Guid ReadBigEndianGuid(byte[] bytes)
        {
            byte[] buffer = new byte[16];
            // copy the first six bytes to the end in reverse order
            CopyBytes(bytes, buffer, 5, 6, 10, -1);

            // copy the next two bytes also in reverse order
            CopyBytes(bytes, buffer, 7, 2, 8, -1);

            // the next two bytes in the same order
            CopyBytes(bytes, buffer, 8, 2, 6, 1);

            // the next two bytes in the same order
            CopyBytes(bytes, buffer, 10, 2, 4, 1);

            // the last four bytes in the same order
            CopyBytes(bytes, buffer, 12, 4, 0, 1);

            return new Guid(buffer);
        }
        protected byte[] WriteBigEndianGuid(Guid guid)
        {
            byte[] bytes = guid.ToByteArray();

            byte[] buffer = new byte[16];

            // copy the last six bytes in reverse order
            CopyBytes(bytes, buffer, 15, 6, 0, -1);

            // copy the next two bytes also in reverse order
            CopyBytes(bytes, buffer, 9, 2, 6, -1);

            // the next two bytes in the same order
            CopyBytes(bytes, buffer, 6, 2, 8, 1);

            // the next two bytes in the same order
            CopyBytes(bytes, buffer, 4, 2, 10, 1);

            // the last four bytes in the same order
            CopyBytes(bytes, buffer, 0, 4, 12, 1);

            return buffer;
        }

        private void CopyBytes(byte[] source, byte[] target, int index, int count, int targetIndex, int targetDirection)
        {
            int end = index + (count * targetDirection);
            for (int i = index; i != end; i += targetDirection)
            {
                byte b = source[i];
                target[targetIndex] = b;
                targetIndex++;
            }
        }


        protected short ReadBigEndian16bit(DataReader reader)
        {
            byte lo = reader.ReadByte();
            byte hi = reader.ReadByte();
            return (short)(((short)hi << 8) + (short)lo);
        }

        protected ushort ReadBigEndianU16bit(DataReader reader)
        {
            byte lo = reader.ReadByte();
            byte hi = reader.ReadByte();
            return (ushort)(((ushort)hi << 8) + (ushort)lo);
        }

        protected void WriteBigEndian16bit(DataWriter writer, ushort value)
        {
            byte a = (byte)((value & 0xff00) >> 8);
            byte b = (byte)value;
            writer.WriteByte(b);
            writer.WriteByte(a);
        }


        protected int ReadBigEndian24bit(DataReader reader)
        {
            byte lo = reader.ReadByte();
            byte hi = reader.ReadByte();
            byte highest = reader.ReadByte();
            return (int)(((int)highest << 8) + ((int)hi << 8) + (int)lo);
        }

        protected uint ReadBigEndianU24bit(DataReader reader)
        {
            byte lo = reader.ReadByte();
            byte hi = reader.ReadByte();
            byte highest = reader.ReadByte();
            return (uint)(((uint)highest << 16) + ((uint)hi << 8) + (uint)lo);
        }

        protected uint ReadBigEndianUint32(DataReader reader)
        {
            byte a = reader.ReadByte();
            byte b = reader.ReadByte();
            byte c = reader.ReadByte();
            byte d = reader.ReadByte();

            return (uint)((d << 24) + (c << 16) + (b << 8) + a);
        }

        protected void WriteBigEndian32bit(DataWriter writer, uint value)
        {
            byte a = (byte)((value & 0xff000000) >> 24);
            byte b = (byte)((value & 0x00ff0000) >> 16);
            byte c = (byte)((value & 0x0000ff00) >> 8);
            byte d = (byte)value;
            writer.WriteByte(d);
            writer.WriteByte(c);
            writer.WriteByte(b);
            writer.WriteByte(a);
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

    public class BleGattDeviceInfo
    {
        string serviceGuid;
        string instanceUuid;
        string vid;
        string pid;
        string rev;
        string macAddress;
        ulong addr;

        public DeviceInformation DeviceInformation { get; private set; }

        public BleGattDeviceInfo(DeviceInformation info)
        {
            this.DeviceInformation = info;

            string id = info.Id;

            // something like this:
            // \\?\BTHLEDevice#{0000fff0-0000-1000-8000-00805f9b34fb}_Dev_VID&01000d_PID&0000_REV&0110_b4994c5d8fc1#7&2839f98&c&0023#{6e3bb679-4372-40c8-9eaa-4509df260cd8}
            if (id.StartsWith(@"\\?\BTHLEDevice#"))
            {
                int i = id.IndexOf('{');
                if (i > 0 && i < id.Length - 1)
                {
                    i++;
                    int j = id.IndexOf('}', i);
                    if (j > i)
                    {
                        serviceGuid = id.Substring(i, j - i);
                        if (j < id.Length - 1)
                        {
                            i = id.IndexOf('{', j);
                            string tail = id.Substring(j + 1);
                            if (i > 0 && i < id.Length - 1)
                            {
                                i++;
                                j = id.IndexOf('}', i);
                                if (j > i)
                                {
                                    instanceUuid = id.Substring(i, j - i);
                                }
                            }
                            i = tail.IndexOf('#');
                            if (i > 0)
                            {
                                tail = tail.Substring(0, i);
                            }
                            string[] parts = tail.Split('_');
                            foreach (string p in parts)
                            {
                                if (p.StartsWith("VID&"))
                                {
                                    this.vid = p.Substring(4);
                                }
                                else if (p.StartsWith("PID&"))
                                {
                                    this.pid = p.Substring(4);
                                }
                                else if (p.StartsWith("REV&"))
                                {
                                    this.pid = p.Substring(4);
                                }
                                else if (p.Length == 12)
                                {
                                    this.MacAddress = p;
                                }
                            }
                        }
                    }
                }
            }
        }

        public string ContainerId
        {
            get
            {
                object o = DeviceInformation.Properties[BleGenericGattService.CONTAINER_ID_PROPERTY_NAME];
                if (o != null)
                {
                    return o.ToString();
                }
                return null;
            }
        }


        public string ServiceGuid
        {
            get { return serviceGuid; }
        }

        public string InstanceUuid
        {
            get { return instanceUuid; }
        }

        public string Vid
        {
            get { return vid; }
        }

        public string Pid
        {
            get { return pid; }
        }

        public string Revision
        {
            get { return rev; }
            set { rev = value; }
        }

        public string MacAddress
        {
            get { return macAddress; }
            set { macAddress = value; ParseMacAddress(value); }
        }

        public ulong Address
        {
            get { return addr; }
            set { addr = value; }
        }

        private void ParseMacAddress(string value)
        {
            ulong.TryParse(value, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.CurrentCulture, out addr);
        }

    }

}
