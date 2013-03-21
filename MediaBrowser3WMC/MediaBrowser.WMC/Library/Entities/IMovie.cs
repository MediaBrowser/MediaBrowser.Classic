using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser.Library.Entities
{
    public interface IMovie
    {
        string Name { get; set; }
        string Overview { get; set; }
        string TagLine { get; set; }
        string ImdbID { get; set; }
        string TmdbID { get; set; }
        MediaInfoData MediaInfo { get; set; }
        List<Actor> Actors { get; set; }
        List<string> Directors { get; set; }
        List<string> Writers { get; set; }
        List<string> Genres { get; set; }
        float? ImdbRating { get; set; }
        string MpaaRating { get; set; }
        string OfficialRating { get; }
        int? RunningTime { get; set; }
        List<string> Studios { get; set; }
        string AspectRatio { get; set; }
        int? ProductionYear { get; set; }
        string DisplayMediaType { get; set; }
        string CustomRating { get; set; }
        string CustomPIN { get; set; }
        string Plot { get; set; }
        string SortName { get; set; }
        string TrailerPath { get; set; }
        DateTime DateCreated { get; set; }
    }
}
