namespace MediaBrowser.Library.Entities
{
    class PodcastsChannel : Channel
    {
        public override bool ForceStaticStream
        {
            get { return true; }
        }
    }
}