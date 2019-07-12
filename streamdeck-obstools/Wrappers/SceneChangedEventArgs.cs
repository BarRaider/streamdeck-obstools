using OBSWebsocketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BarRaider.ObsTools.Wrappers
{
    public class SceneChangedEventArgs : EventArgs
    {
        public String CurrentSceneName { get; private set; }

        public SceneChangedEventArgs(string currentSceneName)
        {
            CurrentSceneName = currentSceneName;
        }
    }
}
