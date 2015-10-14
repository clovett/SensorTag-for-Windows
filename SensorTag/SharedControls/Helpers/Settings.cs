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
        Dictionary<string, string> nameMap = new Dictionary<string, string>();

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

        public string[] Names
        {
            get
            {
                List<string> names = new List<string>();
                foreach (var pair in nameMap)
                {
                    names.Add(pair.Key);
                    names.Add(pair.Value);
                }
                return names.ToArray();
            }
            set
            {
                nameMap.Clear();
                if (value != null)
                {
                    for (int i = 0, n = value.Length; i < n; i += 2)
                    {
                        string key = value[i];
                        string name = (i + 1 < n) ? value[i + 1] : null;
                        nameMap[key] = name;
                    }
                }
                OnPropertyChanged("Names");
            }
        }

        public string FindName(string key)
        {
            string value = null;
            nameMap.TryGetValue(key, out value);
            return value;
        }

        public void SetName(string key, string name)
        {
            nameMap[key] = name;
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

        bool saving;

        public async Task SaveAsync()
        {
            if (!saving)
            {
                saving = true;
                var store = new IsolatedStorage<Settings>();
                await store.SaveToFileAsync(Windows.Storage.ApplicationData.Current.LocalFolder, "settings.xml", this);
            }
            saving = false;
        }

    }
}
