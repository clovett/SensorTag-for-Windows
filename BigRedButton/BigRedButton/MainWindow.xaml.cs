using SensorTag;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;

namespace BigRedButton
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        BleButtonService buttons;
        BleDeviceWatcher watcher;
        bool muted;

        public MainWindow()
        {
            InitializeComponent();
            buttons = new BleButtonService();
            this.Loaded += OnWindowLoaded;
            watcher = new BleDeviceWatcher();
            watcher.StartWatching(Guid.Parse("f000aa00-0451-4000-b000-000000000000"));
        }

        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (AudioManager.IsMute())
            {                
                muted = true;
            }
            UpdateMicIcon();
            await FindPairedDevices();
        }

        public async Task FindPairedDevices()
        {
            foreach (var info in await DeviceInformation.FindAllAsync(GattDeviceService.GetDeviceSelectorFromUuid(GattServiceUuids.DeviceInformation)))
            {
                if (info.Name == "SensorTag 2.0")
                {
                    Debug.WriteLine("Found device: {0}: {1}", info.Name, info.Id);
                    string containerId = info.Properties[BleGenericGattService.CONTAINER_ID_PROPERTY_NAME]?.ToString();
                    await this.ConnectButtonsAsync(containerId);
                }
            }
        }

        private async Task ConnectButtonsAsync(string containerId)
        {
            Debug.WriteLine("connecting...");
            await buttons.ConnectAsync(containerId);
            buttons.ButtonValueChanged += OnButtonValueChanged;        
        }

        bool leftButtonDown;
        bool rightButtonDown;

        private void OnButtonValueChanged(object sender, SensorButtonEventArgs e)
        {
            leftButtonDown = e.LeftButtonDown;
            rightButtonDown = e.RightButtonDown;
            Debug.WriteLine("Button state: left={0}, right={1}", e.LeftButtonDown, e.RightButtonDown);
            this.Dispatcher.Invoke(OnUpdateButtons);
        }

        private void OnUpdateButtons()
        {
            if (leftButtonDown)
            {
                if (AudioManager.SetMute(!muted) == 0)
                {
                    muted = !muted;
                    UpdateMicIcon();
                }
            }

            if (rightButtonDown)
            {
                RightButton.Background = Brushes.Green;
            }
            else
            {
                RightButton.SetValue(Button.BackgroundProperty, DependencyProperty.UnsetValue);
            }
        }

        void UpdateMicIcon()
        {
            if (muted)
            {
                MicOn.Visibility = Visibility.Collapsed;
                MicOff.Visibility = Visibility.Visible;
            }
            else
            {
                MicOn.Visibility = Visibility.Visible;
                MicOff.Visibility = Visibility.Collapsed;
            }
        }
    }
}
