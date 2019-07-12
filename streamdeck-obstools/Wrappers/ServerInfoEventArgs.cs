using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BarRaider.ObsTools.Wrappers
{
    internal class ServerInfoEventArgs : EventArgs
    {
        public ServerInfo ServerInfo { get; private set; }

        public ServerInfoEventArgs(ServerInfo serverInfo)
        {
            ServerInfo = serverInfo;
        }
    }
}
