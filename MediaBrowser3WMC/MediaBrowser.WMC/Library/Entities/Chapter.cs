using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Entities
{
    public class Chapter : BaseItem
    {
        public long PositionTicks { get; set; }

        public override bool PlayAction(Item item)
        {
            var chapterItem = item as ChapterItem;
            if (chapterItem == null)
            {
                Logging.Logger.ReportError("Attempt to play invaild chapter item {0}", item.GetType().Name);
                return false;
            }

            Application.CurrentInstance.Play(chapterItem.ParentItem, PositionTicks);
            return true;
        }
    }
}
