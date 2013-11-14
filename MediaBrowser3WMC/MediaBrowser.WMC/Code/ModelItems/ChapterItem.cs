using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.ImageManagement;
using MediaBrowser.LibraryManagement;
using MediaBrowser.Model.Dto;

namespace MediaBrowser.Library
{
    public class ChapterItem : Item
    {
        public long PositionTicks {get { return Chapter.PositionTicks; }}
        public Chapter Chapter { get { return baseItem as Chapter ?? new Chapter(); } }
        public Item ParentItem { get; set; }

        private string _position;
        public string Position
        {
            get { return _position ?? (_position = Helper.TicksToFriendlyTime(PositionTicks)); }
        }

        public static ChapterItem Create(Chapter chapter, Item parent)
        {
            var item = new ChapterItem();
            item.Assign(chapter);
            item.ParentItem = parent;
            return item;
        }
    }
}
