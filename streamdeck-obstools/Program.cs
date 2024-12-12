using BarRaider.ObsTools.Backend;
using BarRaider.SdTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BarRaider.ObsTools
{
    class Program
    {
        static void Main(string[] args)
        {
            SDWrapper.Run(args, new UpdateHandler());
        }
    }
}
