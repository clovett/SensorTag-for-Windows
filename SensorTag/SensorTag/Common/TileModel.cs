using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media;

namespace SensorTag
{
    public class TileModel : INotifyPropertyChanged
    {
        private ImageSource icon;

        public ImageSource Icon
        {
            get { return icon; }
            set
            {
                if (icon != value)
                {
                    icon = value;
                    OnPropertyChanged("Icon");
                }
            }
        }

        private string caption;

        public string Caption
        {
            get { return caption; }
            set
            {
                if (caption != value)
                {
                    caption = value;
                    OnPropertyChanged("Caption");
                }
            }

        }
            
        private string sensorValue;

        public string SensorValue
        {
            get { return sensorValue; }
            set
            {
                if (sensorValue != value)
                {
                    sensorValue = value;
                    OnPropertyChanged("SensorValue");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
