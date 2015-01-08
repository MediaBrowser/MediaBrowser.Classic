using System;
using System.Collections.Generic;
using System.Linq;

namespace MediaBrowser.Library.Entities
{
    class UtvDateFolder : SearchResultFolder
    {
        private readonly string _yearStr;
        private readonly string _dateStr;

        public UtvDateFolder()
        {
        }

        public UtvDateFolder(DateTime date, IEnumerable<BaseItem> children ) : base(children.ToList())
        {
            PremierDate = date;
            _yearStr = date.Year == DateTime.Now.Year ? "" : "yyyy";
            _dateStr = date.Date < DateTime.Now.AddDays(7).Date ? "" : " d MMMM ";
            Id = Guid.NewGuid();
        }

        public override string Name
        {
            get { return PremierDate.Date == DateTime.Now.AddDays(-1).Date ? "Yesterday" : PremierDate.Date == DateTime.Today ? "Today" : PremierDate.Date == DateTime.Now.AddDays(1).Date ? "Tomorrow" : PremierDate.ToString("dddd"+_dateStr+_yearStr); }
            set
            {
                base.Name = value;
            }
        }
    }
}
