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
    public sealed partial class CompassControl : UserControl
    {
        const double HandWidth = 10;

        public CompassControl()
        {
            this.InitializeComponent();
            this.SizeChanged += CompassControl_SizeChanged;
        }

        void CompassControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double w = e.NewSize.Width;
            double h = e.NewSize.Height;

            // the top hand
            PathGeometry top = new PathGeometry();
            PathFigure f = new PathFigure() { StartPoint = new Point(HandWidth, 0), IsFilled = true };
            top.Figures.Add(f);
            f.Segments.Add(new LineSegment() { Point = new Point(0, h / 2) });
            f.Segments.Add(new LineSegment() { Point = new Point(-HandWidth, 0) });
            TopHand.Data = top;

            // the bottom hand
            PathGeometry bottom = new PathGeometry();
            f = new PathFigure() { StartPoint = new Point(HandWidth, 0), IsFilled = true };
            bottom.Figures.Add(f);
            f.Segments.Add(new LineSegment() { Point = new Point(0, -h / 2) });
            f.Segments.Add(new LineSegment() { Point = new Point(-HandWidth, 0) });
            BottomHand.Data = bottom;

            TranslateTransform.X = TranslateTransform.Y = h / 2;
        }



        public double Angle
        {
            get { return (double)GetValue(AngleProperty); }
            set { SetValue(AngleProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Angle.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty AngleProperty =
            DependencyProperty.Register("Angle", typeof(double), typeof(CompassControl), new PropertyMetadata(0d, new PropertyChangedCallback(OnAngleChanged)));

        private static void OnAngleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((CompassControl)d).OnAngleChanged();
        }

        void OnAngleChanged()
        {
            RotateTransform.Angle = this.Angle;
        }


    }
}
