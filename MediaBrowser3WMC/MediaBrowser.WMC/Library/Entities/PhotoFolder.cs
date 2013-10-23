using MediaBrowser.Library.Localization;

namespace MediaBrowser.Library.Entities
{
    public class PhotoFolder : Folder
    {

        public override bool PlayAction(Item item)
        {
            //assume shuffle
            MBPhotoController.Instance.SlideShow(item, true);
            return true;
        }

    }
}
