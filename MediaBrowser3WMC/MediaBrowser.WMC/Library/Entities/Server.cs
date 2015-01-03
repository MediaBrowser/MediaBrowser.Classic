using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Library.Entities
{
    public class Server : BaseItem
    {
        public ServerInfo Info { get; set; }

        public override string TagLine { get; set; }

        public override bool SelectAction(Item item)
        {
            Application.CurrentInstance.ConnectToServer(item);
            return true;
        }
    }
}
