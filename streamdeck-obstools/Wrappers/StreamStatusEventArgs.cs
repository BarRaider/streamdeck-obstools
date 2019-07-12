using OBSWebsocketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BarRaider.ObsTools.Wrappers
{
    public class StreamStatusEventArgs : EventArgs
    {
        public StreamStatus Status { get; private set; }

        public StreamStatusEventArgs(StreamStatus status)
        {
            Status = status;
        }
    }
}
