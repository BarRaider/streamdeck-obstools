using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatPager.Twitch
{
    public class PageRaisedEventArgs : EventArgs
    {
        public string Message { get; private set; }

        public PageRaisedEventArgs(string message)
        {
            Message = message;
        }
    }
}
