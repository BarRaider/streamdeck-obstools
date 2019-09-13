using System;
using System.Collections.Generic;
using System.Text;

namespace ChatPager.Twitch
{
    public class TwitchTokenEventArgs : EventArgs
    {
        public TwitchToken Token { get; private set; }

        public TwitchTokenEventArgs(TwitchToken token)
        {
            Token = token;
        }
    }
}
