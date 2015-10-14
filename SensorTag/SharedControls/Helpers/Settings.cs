using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SensorTag
{
    public class Settings : INotifyPropertyChanged
    {
        bool _celcius;
        int _pressureUnit;

        static Settings _instance;

        public Settings()
        {
            _instance = this;
        }

        public static Settings Instance
        {
            get
            {
                if (_instance == null)
                {
                    return new Settings();
                }
                return _instance;
            }
        }

        public bool Celcius
        {
            get { return _celcius; }
            set
            {
                if (_celcius != value)
                {
                    _celcius = value;
                    OnPropertyChanged("Celcius");
                }
            }
        }

        public int PressureUnit
        {
            get { return _pressureUnit; }
            set
            {
                if (_pressureUnit != value)
                {
                    _pressureUnit = value;
                    OnPropertyChanged("PressureUnit");
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

        public static async Task<Settings> LoadAsync()
        {
            var store = new IsolatedStorage<Settings>();
            Settings result = null;
            try
            {
                result = await store.LoadFromFileAsync(Windows.Storage.ApplicationData.Current.LocalFolder, "settings.xml");
            }
            catch
            {
            }
            return result;
        }

        public async Task SaveAsync()
        {
            var store = new IsolatedStorage<Settings>();
            await store.SaveToFileAsync(Windows.Storage.ApplicationData.Current.LocalFolder, "settings.xml", this);
        }

    }
}
