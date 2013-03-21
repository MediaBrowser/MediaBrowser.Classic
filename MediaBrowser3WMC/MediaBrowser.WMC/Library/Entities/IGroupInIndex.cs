using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Entities
{
    public interface IGroupInIndex
    {
        IContainer MainContainer { get; }
    }
}
