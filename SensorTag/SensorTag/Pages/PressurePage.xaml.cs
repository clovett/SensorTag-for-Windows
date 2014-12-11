using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkID=390556

namespace SensorTag.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PressurePage : Page
    {
        SensorTag sensor;

        public PressurePage()
        {
            this.InitializeComponent();

            string[] units = new string[] { "hectopascal", "pascal", "bar", "millibar", "kilopascal", "Mercury (mm)", "Mercury (inches)", "psi" };
            UnitCombo.ItemsSource = units;
            UnitCombo.SelectedIndex = 0;

            sensor = SensorTag.Instance;            
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.
        /// This parameter is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            sensor.BarometerMeasurementValueChanged += OnBarometerMeasurementValueChanged;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            sensor.BarometerMeasurementValueChanged -= OnBarometerMeasurementValueChanged;
            base.OnNavigatedFrom(e);
        }

        private void OnBarometerMeasurementValueChanged(object sender, BarometerMeasurementEventArgs e)
        {
            var nowait = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
            {
                string caption = "";
                switch ((string)UnitCombo.SelectedItem)
                {
                    case "hectopascal":
                        caption = GetCaption(e.Measurement.HectoPascals);
                        break;
                    case "pascal":
                        caption = GetCaption(e.Measurement.Pascals);
                        break;
                    case "bar":
                        caption = GetCaption(e.Measurement.Bars);
                        break;
                    case "millibar":
                        caption = GetCaption(e.Measurement.MilliBars);
                        break;
                    case "kilopascal":
                        caption = GetCaption(e.Measurement.KiloPascals);
                        break;
                    case "Mercury (mm)":
                        caption = GetCaption(e.Measurement.HgMm);
                        break;
                    case "Mercury (inches)":
                        caption = GetCaption(e.Measurement.HgInches);
                        break;
                    case "psi":
                        caption = GetCaption(e.Measurement.Psi);
                        break;
                }

                ValueText.Text = caption;
            }));
        }

        string GetCaption(double value)
        {
            return Math.Round(value, 2).ToString();
        }

        private void OnGoBack(object sender, RoutedEventArgs e)
        {
            this.Frame.GoBack();
        }
    }
}
