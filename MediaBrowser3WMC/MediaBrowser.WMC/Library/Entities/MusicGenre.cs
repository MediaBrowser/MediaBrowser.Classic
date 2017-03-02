namespace MediaBrowser.Library.Entities
{
    class MusicGenre : Genre
    {
        public override bool ShowUnwatchedCount
        {
            get { return false; }
        }
    }
}