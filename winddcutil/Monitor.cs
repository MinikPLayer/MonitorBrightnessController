using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace winddcutil
{
    public abstract class Monitor
    {
        public abstract uint GetBrightness();
        public abstract void SetBrightness(uint brightness);

        public static async Task<List<Monitor>?> Detect()
        {
            var list = new List<Monitor>();
            list.AddRange(await MonitorHDR.Detect());
            list.AddRange(await MonitorDDC.Detect());

            return list;
        }
    }
}
