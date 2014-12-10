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

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Microsoft.MobileLabs.Controls
{
    public sealed partial class BatteryLevelControl : UserControl
    {
        double level;
        double maxWidth;

        public BatteryLevelControl()
        {
            this.InitializeComponent();

            maxWidth = FillLevel.Width;
        }

        public double BatteryLevel
        {
            get { return (double)GetValue(BatteryLevelProperty); }
            set { SetValue(BatteryLevelProperty, value); }
        }

        // Using a DependencyProperty as the backing store for BatteryLevel.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty BatteryLevelProperty =
            DependencyProperty.Register("BatteryLevel", typeof(double), typeof(BatteryLevelControl), new PropertyMetadata(0.0, new PropertyChangedCallback(OnBatteryLevelChanged)));

        private static void OnBatteryLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((BatteryLevelControl)d).OnBatteryLevelChanged((double)e.NewValue);
        }

        private void OnBatteryLevelChanged(double newLevel)
        {
            this.level = Math.Min(1, Math.Max(0, newLevel));
            FillLevel.Width = (this.level * maxWidth);
        }
    }
}
