namespace MediaBrowser.Library.Entities
{
    class TrailersChannel : Channel
    {
        public override bool ForceStaticStream
        {
            get { return true; }
        }
    }
}