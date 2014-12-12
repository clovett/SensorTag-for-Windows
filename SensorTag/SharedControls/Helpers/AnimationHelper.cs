using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Animation;

namespace SensorTag
{
    public static class AnimationHelper
    {
        public static Storyboard BeginAnimation(this UIElement element, Timeline animation, string targetProperty, EventHandler<object> completionHandler)
        {
            Storyboard storyboard = new Storyboard();
            storyboard.FillBehavior = FillBehavior.HoldEnd;
            storyboard.Children.Add(animation);
            Storyboard.SetTarget(animation, element);
            Storyboard.SetTargetProperty(animation, targetProperty);
            if (completionHandler != null) 
            {
                storyboard.Completed += completionHandler;
            }
            storyboard.Begin();
            return storyboard;
        }
    }
}
