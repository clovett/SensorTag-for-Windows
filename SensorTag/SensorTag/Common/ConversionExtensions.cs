using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SensorTag
{
    public static class ConversionExtensions
    {
        public static double ToFahrenheit(this double celcius)
        {
            return 32.0 + (celcius * 9) / 5;
        }

    }
}
