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

namespace SensorTag.Controls
{
    public sealed partial class EditableTextBlock : UserControl
    {
        public EditableTextBlock()
        {
            this.InitializeComponent();
        }


        public string Label
        {
            get { return (string)GetValue(LabelProperty); }
            set { SetValue(LabelProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Label.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register("Label", typeof(string), typeof(EditableTextBlock), new PropertyMetadata(null, new PropertyChangedCallback(OnLabelChanged)));

        private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((EditableTextBlock)d).OnLabelChanged();
        }

        void OnLabelChanged()
        {
            LabelTextBlock.Text = LabelEditBox.Text = this.Label;
            if (LabelChanged != null)
            {
                LabelChanged(this, EventArgs.Empty);
            }
        }

        public event EventHandler LabelChanged;


        private void PageTitleEditBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitEdit();
        }

        private void CommitEdit()
        {
            LabelTextBlock.Visibility = Windows.UI.Xaml.Visibility.Visible;
            LabelEditBox.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            Label = LabelEditBox.Text;
        }

        private void PageTitleEditBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                CommitEdit();
            }
        }

        private void OnBorderPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            BeginEdit();
        }

        public void BeginEdit()
        {
            LabelTextBlock.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            LabelEditBox.Visibility = Windows.UI.Xaml.Visibility.Visible;

            var nowait = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
            {
                LabelEditBox.SelectAll();
                LabelEditBox.Focus(Windows.UI.Xaml.FocusState.Programmatic);
            }));
        }
    }
}
