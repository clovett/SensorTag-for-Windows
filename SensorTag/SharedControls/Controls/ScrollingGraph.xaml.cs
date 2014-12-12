using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace SensorTag.Controls
{
    public partial class ScrollingGraph : UserControl
    {
        List<double> pending = new List<double>();
        List<double> history = new List<double>();
        List<Line> gridLines = new List<Line>();
        List<Path> lineGraphs = new List<Path>();
        List<Path> shadowLineGraphs = new List<Path>();

        DispatcherTimer layoutTimer;
        bool scrollTimerStopped;
        DispatcherTimer scrollTimer;
        double previousValue;
        double currentValue;
        bool sizeChanged;
        Size graphSize;
        int layoutDelay = 30; // can't seem to update the Path object more frequently than this.
        int x;
        int totalX;
        bool started;
        bool canRun;
        const int MaxLineGraphLength = 100;
        Path activeLineGraph;
        Path activeShadowLineGraph;

        public ScrollingGraph()
        {
            InitializeComponent();

            RootBorder.SetBinding(Border.BackgroundProperty, new Binding() { Source = this, Path = new PropertyPath("Background") });
            RootBorder.SetBinding(Border.BorderBrushProperty, new Binding() { Source = this, Path = new PropertyPath("BorderBrush") });
            RootBorder.SetBinding(Border.BorderThicknessProperty, new Binding() { Source = this, Path = new PropertyPath("BorderThickness") });

            this.LayoutUpdated += Chart_LayoutUpdated;

            this.SizeChanged += ScrollingGraph_SizeChanged;

            this.Unloaded += ScrollingGraph_Unloaded;

        }

        private Path CreateLineGraph()
        {
            Brush brush = this.Stroke;
            if (brush == null)
            {
                brush = new SolidColorBrush(Windows.UI.Colors.Green);
            }

            var lineGraph = new Path();
            lineGraph.StrokeThickness = StrokeThickness;
            lineGraph.StrokeLineJoin = PenLineJoin.Round;
            lineGraph.Stroke = brush;
            lineGraph.StrokeLineJoin = PenLineJoin.Round;
            GraphCanvas.Children.Add(lineGraph);
            this.lineGraphs.Add(lineGraph);
            return lineGraph;
        }

        private Path CreateShadowLineGraph()
        {
            Brush brush = this.ShadowStroke;
            if (brush == null)
            {
                brush = new SolidColorBrush(Windows.UI.Colors.Green);
            }

            var shadowLineGraph = new Path();
            shadowLineGraph.StrokeThickness = StrokeThickness;
            shadowLineGraph.StrokeLineJoin = PenLineJoin.Round;
            shadowLineGraph.Stroke = this.ShadowStroke;
            shadowLineGraph.StrokeLineJoin = PenLineJoin.Round;
            GraphCanvas.Children.Add(shadowLineGraph);
            this.shadowLineGraphs.Add(shadowLineGraph);
            return shadowLineGraph;
        }

        private void ConnectGraphs(Path p1, Path p2)
        {
            if (p1 != null && p2 != null)
            {
                double x2 = Canvas.GetLeft(p1) + MaxLineGraphLength;
                Canvas.SetLeft(p2, x2);

                PathGeometry previous = p1.Data as PathGeometry;
                if (previous != null)
                {
                    PathFigure figure = previous.Figures.FirstOrDefault();
                    if (figure != null)
                    {
                        LineSegment lastSegment = figure.Segments.LastOrDefault() as LineSegment;
                        if (lastSegment != null)
                        {
                            // make the new path start at the same point the last graph finished at.
                            double y = lastSegment.Point.Y;
                            PathGeometry data = new PathGeometry();
                            var f = new PathFigure() { StartPoint = new Point(0, y), IsFilled = false, IsClosed = false };
                            data.Figures.Add(f);
                            p2.Data = data;
                        }
                    }
                }
            }

        }

        private Path GetOrCreateActiveLineGraph()
        {
            if (this.activeLineGraph == null)
            {
                Path previousLineGraph = this.lineGraphs.LastOrDefault();
                Path previousShadowGraph = this.shadowLineGraphs.LastOrDefault();

                this.activeShadowLineGraph = CreateShadowLineGraph(); // underneath
                this.activeLineGraph = CreateLineGraph();

                ConnectGraphs(previousLineGraph, this.activeLineGraph);
                ConnectGraphs(previousShadowGraph, this.activeShadowLineGraph);

                Debug.WriteLine("Adding new line graph at x={0}", Canvas.GetLeft(this.activeLineGraph));
                Debug.WriteLine("Adding new shadow graph at x={0}", Canvas.GetLeft(this.activeShadowLineGraph));

            }
            return this.activeLineGraph;
        }

        private Path GetOrCreateActiveShadowLineGraph()
        {
            GetOrCreateActiveLineGraph();
            return this.activeShadowLineGraph;
        }

        private void RemoveShadowLineGraph(Path p)
        {
            this.shadowLineGraphs.Remove(p);
            this.GraphCanvas.Children.Remove(p);
        }

        private void RemoveLineGraph(Path p)
        {
            Debug.WriteLine("Remove line graph");
            this.lineGraphs.Remove(p);
            this.GraphCanvas.Children.Remove(p);
        }


        void ScrollingGraph_Unloaded(object sender, RoutedEventArgs e)
        {
            canRun = false;
            Stop();
        }

        /// <summary>
        /// Set the current value that we are showing in the graph, this value will scroll across the screen at the
        /// ScrollingSpeed until a new value is specified.  So the smoothness of the graph all depends on how quickly
        /// you call this method.  
        /// </summary>
        /// <param name="value"></param>
        public void SetCurrentValue(double value)
        {
            previousValue = currentValue;
            currentValue = value;
        }

        /// <summary>
        /// Start scrolling the current values.
        /// </summary>
        public void Start()
        {
            this.started = true;
            if (this.canRun)
            {
                StartAnimating();
            }
        }

        /// <summary>
        /// Stop scrolling.
        /// </summary>
        public void Stop()
        {
            this.started = false;

            if (layoutTimer != null)
            {
                layoutTimer.Tick -= OnLayoutTimer;
                layoutTimer.Stop();
                layoutTimer = null;
            }
            StopScrollTimer();
        }



        public int ScrollSpeed
        {
            get { return (int)GetValue(ScrollSpeedProperty); }
            set { SetValue(ScrollSpeedProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ScrollSpeed.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ScrollSpeedProperty =
            DependencyProperty.Register("ScrollSpeed", typeof(int), typeof(ScrollingGraph), new PropertyMetadata(0, new PropertyChangedCallback(OnScrollSpeedChanged)));

        private static void OnScrollSpeedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ScrollingGraph)d).OnScrollSpeedChanged();
        }

        private void OnScrollSpeedChanged()
        {
            if (scrollTimer != null)
            {
                StopScrollTimer();
                StartScrollTimer();
            }
        }

        private void StartScrollTimer()
        {
            var scrollSpeed = Math.Max(1, this.ScrollSpeed);
            scrollTimerStopped = false;
            scrollTimer = new DispatcherTimer();
            scrollTimer.Interval = TimeSpan.FromMilliseconds(scrollSpeed);
            scrollTimer.Tick += OnScrollData;
            scrollTimer.Start();
        }

        void StopScrollTimer()
        {
            if (scrollTimer != null)
            {
                scrollTimerStopped = true;
                scrollTimer.Tick -= OnScrollData;
                scrollTimer.Stop();
                scrollTimer = null;
            }
        }



        public double Minimum
        {
            get { return (double)GetValue(MinimumProperty); }
            set { SetValue(MinimumProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Minimum.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register("Minimum", typeof(double), typeof(ScrollingGraph), new PropertyMetadata(0.0));



        public double Maximum
        {
            get { return (double)GetValue(MaximumProperty); }
            set { SetValue(MaximumProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Maximum.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register("Maximum", typeof(double), typeof(ScrollingGraph), new PropertyMetadata(1.0));

        void ScrollingGraph_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            sizeChanged = true;
            graphSize = e.NewSize;
            if (this.activeLineGraph != null && this.history.Count > 0)
            {
                UpdateChart();
            }
        }

        private void SetupGridLines()
        {
            foreach (Line line in gridLines)
            {
                GraphCanvas.Children.Remove(line);
            }
            gridLines.Clear();

            int count = this.GridLines;
            if (count > 0)
            {
                double height = graphSize.Height;
                double range = Maximum - Minimum;
                double step = height / (double)count;
                double y = 0;
                for (int i = 0; i < count; i++)
                {
                    Line line = new Line();
                    line.X1 = 0; line.X2 = graphSize.Width;
                    line.Y1 = y; line.Y2 = y;
                    line.Stroke = GridLineStroke;
                    line.StrokeThickness = 1;
                    GraphCanvas.Children.Add(line);
                    gridLines.Add(line);
                    y += step;
                }
            }
        }

        public double StrokeThickness
        {
            get { return (double)GetValue(StokeThicknessProperty); }
            set { SetValue(StokeThicknessProperty, value); }
        }

        // Using a DependencyProperty as the backing store for StokeThickness.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty StokeThicknessProperty =
            DependencyProperty.Register("StrokeThickness", typeof(double), typeof(ScrollingGraph), new PropertyMetadata(0.0, new PropertyChangedCallback(OnStrokeThicknessChanged)));

        private static void OnStrokeThicknessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ScrollingGraph)d).OnStrokeThicknessChanged();
        }

        private void OnStrokeThicknessChanged()
        {
            foreach (Path p in this.lineGraphs)
            {
                p.StrokeThickness = this.StrokeThickness;
            }

            foreach (Path p in this.shadowLineGraphs)
            {
                p.StrokeThickness = this.StrokeThickness;
            }

        }

        public Brush Stroke
        {
            get { return (Brush)GetValue(StrokeProperty); }
            set { SetValue(StrokeProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Stroke.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty StrokeProperty =
            DependencyProperty.Register("Stroke", typeof(Brush), typeof(ScrollingGraph), new PropertyMetadata(null, new PropertyChangedCallback(OnStrokeChanged)));

        private static void OnStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ScrollingGraph)d).OnStrokeChanged();
        }

        private void OnStrokeChanged()
        {
            foreach (Path p in this.lineGraphs)
            {
                p.Stroke = this.Stroke;
            }
        }


        public Brush ShadowStroke
        {
            get { return (Brush)GetValue(ShadowStrokeProperty); }
            set { SetValue(ShadowStrokeProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Stroke.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ShadowStrokeProperty =
            DependencyProperty.Register("ShadowStroke", typeof(Brush), typeof(ScrollingGraph), new PropertyMetadata(null, new PropertyChangedCallback(OnShadowStrokeChanged)));

        private static void OnShadowStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ScrollingGraph)d).OnShadowStrokeChanged();
        }

        private void OnShadowStrokeChanged()
        {
            foreach (Path p in this.shadowLineGraphs)
            {
                p.Stroke = this.ShadowStroke;
            }
        }


        public double ShadowDepth
        {
            get { return (double)GetValue(ShadowDepthProperty); }
            set { SetValue(ShadowDepthProperty, value); }
        }

        // Using a DependencyProperty as the backing store for StokeThickness.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ShadowDepthProperty =
            DependencyProperty.Register("ShadowDepth", typeof(double), typeof(ScrollingGraph), new PropertyMetadata(1.0));


        public Brush GridLineStroke
        {
            get { return (Brush)GetValue(GridLineStrokeProperty); }
            set { SetValue(GridLineStrokeProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Stroke.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty GridLineStrokeProperty =
            DependencyProperty.Register("GridLineStroke", typeof(Brush), typeof(ScrollingGraph), new PropertyMetadata(null, new PropertyChangedCallback(OnGridLineStrokeChanged)));

        private static void OnGridLineStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ScrollingGraph)d).OnGridLineStrokeChanged();
        }

        private void OnGridLineStrokeChanged()
        {
            foreach (Line line in gridLines)
            {
                line.Stroke = GridLineStroke;
            }
        }


        public double GridLineStrokeThickness
        {
            get { return (double)GetValue(GridLineStrokeThicknessProperty); }
            set { SetValue(GridLineStrokeThicknessProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Stroke.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty GridLineStrokeThicknessProperty =
            DependencyProperty.Register("GridLineStrokeThickness", typeof(Brush), typeof(ScrollingGraph), new PropertyMetadata(0.5, new PropertyChangedCallback(OnGridLineStrokeThicknessChanged)));

        private static void OnGridLineStrokeThicknessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ScrollingGraph)d).OnGridLineStrokeThicknessChanged();
        }

        private void OnGridLineStrokeThicknessChanged()
        {
            foreach (Line line in gridLines)
            {
                line.StrokeThickness = GridLineStrokeThickness;
            }
        }


        /// <summary>
        /// Set the number of grid lines to show between minimum and maximum.
        /// </summary>
        public int GridLines
        {
            get { return (int)GetValue(GridLinesProperty); }
            set { SetValue(GridLinesProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Stroke.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty GridLinesProperty =
            DependencyProperty.Register("GridLines", typeof(Brush), typeof(ScrollingGraph), new PropertyMetadata(0, new PropertyChangedCallback(OnGridLinesChanged)));

        private static void OnGridLinesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ScrollingGraph)d).OnGridLinesChanged();
        }

        private void OnGridLinesChanged()
        {
            sizeChanged = true;
        }


        /// <summary>
        /// Get the recorded history of visible data values (unscaled).
        /// (doesn't go back further than one screen)
        /// </summary>
        public List<double> History { get { return this.history; } }

        public void Clear()
        {
            Reset();
            history.Clear();
        }

        void Reset()
        {
            totalX = 0;
            x = 0;
            foreach (Path p in this.lineGraphs)
            {
                GraphCanvas.Children.Remove(p);
            }
            this.lineGraphs.Clear();
            this.activeLineGraph = null;

            foreach (Path p in this.shadowLineGraphs)
            {
                GraphCanvas.Children.Remove(p);
            }
            this.shadowLineGraphs.Clear();
            this.activeShadowLineGraph = null;
        }


        private void Chart_LayoutUpdated(object sender, object e)
        {
            canRun = true;
            if (this.started)
            {
                StartAnimating();
            }
        }

        void StartAnimating()
        {
            if (layoutTimer != null)
            {
                layoutTimer.Start();
            }
            else
            {
                layoutTimer = new DispatcherTimer();
                layoutTimer.Interval = TimeSpan.FromMilliseconds(layoutDelay);
                layoutTimer.Tick += OnLayoutTimer;
                layoutTimer.Start();
                StopScrollTimer();
                StartScrollTimer();
            }
        }

        private void OnScrollData(object sender, object args)
        {
            if (scrollTimerStopped)
            {
                return;
            }
            int now = Environment.TickCount;

            pending.Add(currentValue);
            history.Add(currentValue);

            if (totalX > graphSize.Width)
            {                
                foreach (Path p in this.lineGraphs.ToArray())
                {
                    double x = Canvas.GetLeft(p) - 1;
                    if (x < -MaxLineGraphLength)
                    {
                        RemoveLineGraph(p);
                    }
                    else
                    {
                        Canvas.SetLeft(p, x);
                    }
                }

                foreach (Path p in this.shadowLineGraphs.ToArray())
                {
                    double x = Canvas.GetLeft(p) - 1;
                    if (x < -MaxLineGraphLength)
                    {
                        RemoveShadowLineGraph(p);
                    }
                    else
                    {
                        Canvas.SetLeft(p, x);
                    }
                }
            }
        }

        private void OnLayoutTimer(object sender, object e)
        {
            UpdateChart();
        }

        private List<double> Tail(List<double> list, int tailLength)
        {
            var result = new List<double>();
            int start = 0;
            int len = this.history.Count;
            if (len > tailLength)
            {
                start = len - tailLength;
            }
            for (int i = start; i < len; i++)
            {
                result.Add(history[i]);
            }
            return result;
        }

        private void UpdateChart()
        {
            double maxHorizontal = graphSize.Width;

            List<double> values = this.pending;
            this.pending = new List<double>();

            if (sizeChanged)
            {
                Reset();
                SetupGridLines();
                values = Tail(this.history, (int)maxHorizontal);
                sizeChanged = false;
            }

            double shadowYOffset = ShadowDepth;
            double shadowXOffset = -ShadowDepth/2;

            HashSet<Path> modified = new HashSet<Path>();

            foreach (double v in values)
            {
                Path lineGraph = GetOrCreateActiveLineGraph();
                Path shadowLineGraph = GetOrCreateActiveShadowLineGraph();
                
                if (lineGraph.Data == null)
                {
                    lineGraph.Data = new PathGeometry();
                }
                if (shadowLineGraph.Data == null)
                {
                    shadowLineGraph.Data = new PathGeometry();
                }
                modified.Add(lineGraph);
                modified.Add(shadowLineGraph);

                PathGeometry g = (PathGeometry)lineGraph.Data;
                PathGeometry sg = (PathGeometry)shadowLineGraph.Data;
                PathFigure f = g.Figures.FirstOrDefault();
                PathFigure sf = sg.Figures.FirstOrDefault();

                Point minLabelPos = new Point(-100, 0);
                Point maxLabelPos = new Point(-100, 0);
                Point minLabelConnector = new Point(-100, 0);
                Point maxLabelConnector = new Point(-100, 0);

                double height = graphSize.Height;
                double min = this.Minimum;
                double max = this.Maximum;
                double range = (max - min);
                if (range == 0) range = 1;

                // add the new values to the path
                double value = v;

                double y = height - ((value - min) * height / range);
                if (f == null)
                {
                    f = new PathFigure() { StartPoint = new Point(x, y), IsFilled = false, IsClosed = false };
                    g.Figures.Add(f);
                }
                else
                {
                    f.Segments.Add(new LineSegment() { Point = new Point(x, y) });
                }

                // shadow
                if (sf == null)
                {
                    sf = new PathFigure() { StartPoint = new Point(x + shadowXOffset, y + shadowYOffset), IsFilled = false, IsClosed = false };
                    sg.Figures.Add(sf);
                }
                else
                {
                    sf.Segments.Add(new LineSegment() { Point = new Point(x + shadowXOffset, y + shadowYOffset) });
                }

                x++;
                totalX++;

                if (f.Segments.Count > MaxLineGraphLength)
                {
                    x = 0;

                    activeLineGraph = null;
                    activeShadowLineGraph = null;
                }
            }

            // reassign the data to force a visual update.
            foreach (Path path in modified)
            {
                path.Data = path.Data;
            }
        }
    }

    static class RectExtensions
    {
        public static Point Center(this Rect r)
        {
            return new Point(r.Left + (r.Width / 2), r.Top + (r.Height / 2));
        }

        public static bool IntersectsWith(this Rect r1, Rect r2)
        {
            Rect r = r1;
            r.Intersect(r2);
            return !r.IsEmpty;
        }
    }
}
