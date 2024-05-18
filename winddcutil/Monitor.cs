using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace winddcutil
{
    public abstract class Monitor
    {
        public abstract uint GetExtendedMax();
        public abstract uint GetTypicalMax();
        public abstract uint GetBrightness();
        public abstract void SetBrightness(uint brightness);

        public static async Task<List<Monitor>?> Detect()
        {
            var list = new List<Monitor>();
            list.AddRange(await MonitorHDR.Detect());
            if(list.Count == 0)
                list.AddRange(await MonitorDDC.Detect());

            return list;
        }
    }
}
