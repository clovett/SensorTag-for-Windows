using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace SensorTag.Controls
{

    /// <summary>
    /// A content control designed to wrap anything in Silverlight with a user
    /// experience concept called 'tilt', applying a transformation during 
    /// manipulation by a user.
    /// </summary>
    public class TiltContentControl : ContentControl
    {
        /// <summary>
        /// The content element instance.
        /// </summary>
        ContentPresenter _presenter;

        /// <summary>
        /// The original width of the control.
        /// </summary>
        double _width;

        /// <summary>
        /// The original height of the control.
        /// </summary>
        double _height;

        /// <summary>
        /// The storyboard used for the tilt up effect.
        /// </summary>
        Storyboard _tiltUpStoryboard;

        /// <summary>
        /// The plane projection used to show the tilt effect.
        /// </summary>
        PlaneProjection _planeProjection;

        /// <summary>
        /// Maximum angle for the tilt effect, defined in Radians.
        /// </summary>
        const double MaxAngle = 0.3;

        /// <summary>
        /// The maximum depression for the tilt effect, given in pixel units.
        /// </summary>
        const double MaxDepression = 25;

        bool _tilting;
        const double TiltThreshold = 5;

        public TiltContentControl()
        {
            //this.ManipulationMode = ManipulationModes.All;
        }


        /// <summary>
        /// The number of seconds for a tilt revert to take.
        /// </summary>
        static Duration TiltUpAnimationDuration
        {
            get
            {
                return new Duration(TimeSpan.FromMilliseconds(300));
            }
        }

        public bool Tilting
        {
            get
            {
                return _tilting;
            }
        }


        /// <summary>
        /// Overrides the method called when apply template is called. We assume
        /// that the implementation root is the content presenter.
        /// </summary>
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _presenter = GetImplementationRoot(this) as ContentPresenter;
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            base.OnPointerPressed(e);
            PointerPoint pt = e.GetCurrentPoint(this);
            StartTilt(pt.Position, this);
            e.Handled = false;
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            base.OnPointerReleased(e);
            StopTilt();
            e.Handled = false;
        }

        /// <summary>
        /// Overrides the maniupulation started event.
        /// </summary>
        /// <param name="e">The manipulation event arguments.</param>
        protected override void OnManipulationStarted(ManipulationStartedRoutedEventArgs e)
        {
            base.OnManipulationStarted(e);
            StartTilt(e.Position, e.Container);
            e.Handled = false;
        }

        /// <summary>
        /// Handles the manipulation delta event.
        /// </summary>
        /// <param name="e">The manipulation event arguments.</param>
        protected override void OnManipulationDelta(ManipulationDeltaRoutedEventArgs e)
        {
            base.OnManipulationDelta(e);

            // Depress and tilt regardless of whether the event was handled.
            if (_planeProjection != null && e.Container != null)
            {
                if (_tilting || Math.Abs(e.Cumulative.Translation.X) > TiltThreshold || Math.Abs(e.Cumulative.Translation.Y) > TiltThreshold)
                {
                    _tilting = true;
                    DepressAndTilt(e.Position, e.Container);
                }
            }
            e.Handled = false;
        }

        /// <summary>
        /// Handles the manipulation completed event.
        /// </summary>
        /// <param name="e">The manipulation event arguments.</param>
        protected override void OnManipulationCompleted(ManipulationCompletedRoutedEventArgs e)
        {
            base.OnManipulationCompleted(e);
            StopTilt();
            e.Handled = false;
        }



        private void StartTilt(Point position, UIElement container)
        {
            _tilting = false;

            if (_presenter != null && _planeProjection == null)
            {
                _planeProjection = new PlaneProjection();
                _presenter.Projection = _planeProjection;

                _tiltUpStoryboard = new Storyboard();
                _tiltUpStoryboard.Completed += new EventHandler<object>(TiltUpCompleted);

                PowerEase ease = new PowerEase();
                ease.Power = 2;

                Duration duration = TiltUpAnimationDuration;

                DoubleAnimation tiltUpRotateXAnimation = new DoubleAnimation();
                Storyboard.SetTarget(tiltUpRotateXAnimation, _planeProjection);
                Storyboard.SetTargetProperty(tiltUpRotateXAnimation, "RotationX");
                tiltUpRotateXAnimation.To = 0;
                tiltUpRotateXAnimation.EasingFunction = ease;
                tiltUpRotateXAnimation.Duration = duration;

                DoubleAnimation tiltUpRotateYAnimation = new DoubleAnimation();
                Storyboard.SetTarget(tiltUpRotateYAnimation, _planeProjection);
                Storyboard.SetTargetProperty(tiltUpRotateYAnimation, "RotationY");
                tiltUpRotateYAnimation.To = 0;

                tiltUpRotateYAnimation.EasingFunction = ease;
                tiltUpRotateYAnimation.Duration = duration;

                DoubleAnimation tiltUpOffsetZAnimation = new DoubleAnimation();
                Storyboard.SetTarget(tiltUpOffsetZAnimation, _planeProjection);
                Storyboard.SetTargetProperty(tiltUpOffsetZAnimation, "GlobalOffsetZ");
                tiltUpOffsetZAnimation.To = 0;
                tiltUpOffsetZAnimation.EasingFunction = ease;
                tiltUpOffsetZAnimation.Duration = duration;

                _tiltUpStoryboard.Children.Add(tiltUpRotateXAnimation);
                _tiltUpStoryboard.Children.Add(tiltUpRotateYAnimation);
                _tiltUpStoryboard.Children.Add(tiltUpOffsetZAnimation);
            }
            if (_planeProjection != null)
            {
                _width = ActualWidth;
                _height = ActualHeight;
                if (_tiltUpStoryboard != null)
                {
                    _tiltUpStoryboard.Stop();
                }
                DepressAndTilt(position, container);
            }
        }



        public void StopTilt()
        {
            if (_planeProjection != null)
            {
                if (_tiltUpStoryboard != null)
                {
                    _tiltUpStoryboard.Begin();
                }
                else
                {
                    _planeProjection.RotationY = 0;
                    _planeProjection.RotationX = 0;
                    _planeProjection.GlobalOffsetZ = 0;
                }
            }
        }



        /// <summary>
        /// Updates the depression and tilt based on position of the 
        /// manipulation relative to the original origin from input.
        /// </summary>
        /// <param name="manipulationOrigin">The origin of manipulation.</param>
        /// <param name="manipulationContainer">The container instance.</param>
        private void DepressAndTilt(Point manipulationOrigin, UIElement manipulationContainer)
        {	
            GeneralTransform transform = manipulationContainer.TransformToVisual(this);
	        Point transformedOrigin = transform.TransformPoint(manipulationOrigin);
	        Point normalizedPoint = new Point(
		        (float)Math.Min(Math.Max(transformedOrigin.X / _width, 0.0), 1.0),
		        (float)Math.Min(Math.Max(transformedOrigin.Y / _height, 0.0), 1.0));
	        double xMagnitude = Math.Abs(normalizedPoint.X - 0.5);
	        double yMagnitude = Math.Abs(normalizedPoint.Y - 0.5);
	        double xDirection = -Math.Sign(normalizedPoint.X - 0.5);
	        double yDirection = Math.Sign(normalizedPoint.Y - 0.5);
	        double angleMagnitude = xMagnitude + yMagnitude;
	        double xAngleContribution = xMagnitude + yMagnitude > 0 ? xMagnitude / (xMagnitude + yMagnitude) : 0;
	        double angle = angleMagnitude * MaxAngle * 180 / Math.PI;
	        double depression = (1 - angleMagnitude) * MaxDepression;
	        // RotationX and RotationY are the angles of rotations about the x- 
	        // or y-axis. To achieve a rotation in the x- or y-direction, the
	        // two must be swapped. So a rotation to the left about the y-axis 
	        // is a rotation to the left in the x-direction, and a rotation up 
	        // about the x-axis is a rotation up in the y-direction.
	        _planeProjection.RotationY = angle * xAngleContribution * xDirection;
	        _planeProjection.RotationX = angle * (1 - xAngleContribution) * yDirection;
	        _planeProjection.GlobalOffsetZ = -depression;
        }

        /// <summary>
        /// Handles the tilt up completed event.
        /// </summary>
        /// <param name="sender">The source object.</param>
        /// <param name="e">The event argument.</param>
        private void TiltUpCompleted(Object sender, Object e)
        {
            if (_tiltUpStoryboard != null)
            {
                _tiltUpStoryboard.Stop();
            }
            _tiltUpStoryboard = null;
            _planeProjection = null;
            _presenter.Projection = null;

            if (TiltCompleted != null)
            {
                TiltCompleted(this, EventArgs.Empty);
            }
        }

        public event EventHandler TiltCompleted ;

        /// <summary>
        /// Gets the implementation root of the Control.
        /// </summary>
        /// <param name="dependencyObject">The DependencyObject.</param>
        /// <remarks>
        /// Implements Silverlight's corresponding internal property on Control.
        /// </remarks>
        /// <returns>Returns the implementation root or null.</returns>
        private static FrameworkElement GetImplementationRoot(DependencyObject dependencyObject)
        {
	        return (1 == VisualTreeHelper.GetChildrenCount(dependencyObject)) ?
		        (FrameworkElement)VisualTreeHelper.GetChild(dependencyObject, 0) : null;
        }
    }
}
