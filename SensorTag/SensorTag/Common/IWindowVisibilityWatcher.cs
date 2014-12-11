using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SensorTag
{
    interface IWindowVisibilityWatcher
    {
        void OnVisibilityChanged(bool visible);
    }
}
