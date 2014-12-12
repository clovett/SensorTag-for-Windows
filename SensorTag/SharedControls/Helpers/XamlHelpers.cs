using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace SensorTag
{
    public static class XamlHelpers
    {
        public static T FindDescendant<T>(this DependencyObject e) where T: class
        {
            int c = VisualTreeHelper.GetChildrenCount(e);
            for (int i = 0; i < c; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(e, i);
                T result = child as T;
                if (result == null)
                {
                    // recurrse
                    result = child.FindDescendant<T>();
                }
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }
    }
}
