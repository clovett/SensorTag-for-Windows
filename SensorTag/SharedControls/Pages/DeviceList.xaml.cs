using SensorTag.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace SensorTag.Pages
{
    public sealed partial class DeviceList : UserControl
    {
        ObservableCollection<TileModel> tiles = new ObservableCollection<TileModel>();

        public DeviceList()
        {
            this.InitializeComponent();

            SensorList.ItemsSource = tiles;
            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
        }

        async void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (args.VirtualKey == Windows.System.VirtualKey.F5)
            {
                await this.FindSensors();
            }
        }

        public async void Show()
        {
            await this.FindSensors();
        }

        public void Hide()
        {

        }

        bool finding;

        private async Task FindSensors()
        {
            try
            {
                if (finding)
                {
                    return;
                }
                finding = true;

                HideHelp();

                tiles.Clear();

                // find a matching sensor
                // todo: let user choose which one to play with.
                foreach (SensorTag tag in await SensorTag.FindAllDevices())
                {
                    string icon = tag.Version == 1 ? "ms-appx:/Assets/SensorTag.png" : "ms-appx:/Assets/ti-sensortag-cc2650.png";
                    
                    string name = Settings.Instance.FindName(tag.DeviceAddress);
                    if (string.IsNullOrEmpty(name))
                    {
                        name = tag.DeviceName;
                    }

                    tiles.Add(new TileModel() { Caption = name, Icon = new BitmapImage(new Uri(icon)), UserData = tag });
                }

                if (tiles.Count == 0)
                {
                    ShowHelp();
                }

            }
            catch (Exception ex)
            {
                DisplayMessage("Finding devices failed, please make sure your Bluetooth radio is on.  Details: " + ex.Message);
                ShowHelp();
            }

            finding = false;
        }

        private void ShowHelp()
        {
            Help.Visibility = Windows.UI.Xaml.Visibility.Visible;
        }

        private void HideHelp()
        {
            Help.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
        }

        private void DisplayMessage(string message)
        {
            var nowait = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
            {
                ErrorMessage.Text = message;
            }));
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            TileModel tile = e.ClickedItem as TileModel;
            SensorTag sensor = (SensorTag)tile.UserData;
            SensorTag.SelectedSensor = sensor;
            Frame frame = Window.Current.Content as Frame;
            frame.Navigate(typeof(DevicePage), sensor);
        }

        private async void OnRefresh(object sender, RoutedEventArgs e)
        {
            RefreshButton.IsEnabled = false;
            await this.FindSensors();
            RefreshButton.IsEnabled = true;
        }
    }
}
