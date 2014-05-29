using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Code.ModelItems;
using MediaBrowser.LibraryManagement;

namespace MediaBrowser.Library
{
    public class SeekPositionItem : BaseModelItem
    {
        public long PositionTicks { get; set; }
        public Item Parent { get; set; }
        public int? ChapterIndex { get; set; }
        public bool IsCurrentPosition { get; set; }
        public bool IsChapterPoint { get { return ChapterIndex != null; } }

        //For mcml
        public SeekPositionItem()
        {
        }

        public SeekPositionItem(long ticks, Item parent)
        {
            PositionTicks = ticks;
            Parent = parent;
        }

        private string _positionString;
        public string PositionString
        {
            get { return _positionString ?? (_positionString = Helper.TicksToFriendlyTime(PositionTicks)); }
        }

        public string ChapterName
        {
            get { return IsChapterPoint ? Parent.Chapters[ChapterIndex.Value].Name : ""; }
        }
    }
}
