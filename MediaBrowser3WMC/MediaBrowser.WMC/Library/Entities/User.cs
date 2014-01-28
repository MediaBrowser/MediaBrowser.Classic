using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Library.Entities
{
    public class User : BaseItem
    {
        public UserDto Dto { get; set; }
        public bool HasPassword { get { return Dto.HasPassword; }}
        public string PwHash { get; set; }

        public override string PrimaryImagePath
        {
            get { return Dto.HasPrimaryImage ? Kernel.ApiClient.GetUserImageUrl(Dto.Id, new ImageOptions {ImageType = ImageType.Primary, Tag = Dto.PrimaryImageTag}) : base.PrimaryImagePath; }
            set
            {
                base.PrimaryImagePath = value;
            }
        }

        public override string TagLine { get; set; }

        public override bool SelectAction(Item item)
        {
            Application.CurrentInstance.LoginUser(item);
            return true;
        }
    }
}
