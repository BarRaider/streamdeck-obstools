using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BarRaider.ObsTools.Wrappers
{
    [Serializable]
    internal class ServerInfo
    {
        public string Ip { get; set; }

        public string Port { get; set; }

        public string Password { get; set; }

        public DateTime TokenLastRefresh { get; set; }
    }
}
