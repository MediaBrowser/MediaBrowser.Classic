using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Diagnostics;
using MediaBrowser.Library.Entities;

namespace MediaBrowser.Library
{
    public class PlaybackStatusFactory 
    {
        public static readonly PlaybackStatusFactory Instance = new PlaybackStatusFactory();

        private PlaybackStatusFactory()
        {
        }

        public PlaybackStatus Create(Guid id)
        {
            PlaybackStatus mine = new PlaybackStatus();
            mine.Id = id;
            return mine;
        }

      
    }
}
