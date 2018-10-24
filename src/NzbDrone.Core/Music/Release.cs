using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using System;
using System.Collections.Generic;
using System.Linq;
using Marr.Data;

namespace NzbDrone.Core.Music
{
    public class Release : ModelBase
    {
        public const string RELEASE_DATE_FORMAT = "yyyy-MM-dd";

        // These correspond to columns in the Releases table
        public int ReleaseGroupId { get; set; }
        public string ForeignReleaseId { get; set; }
        public string Title { get; set; }
        public string Status { get; set; }
        public int Duration { get; set; }
        public List<string> Label { get; set; }
        public string Disambiguation { get; set; }
        public List<string> Country { get; set; }
        public List<Medium> Media { get; set; }
        public int TrackCount { get; set; }

        // These are dynamically queried from other tables
        public LazyLoaded<Album> ReleaseGroup { get; set; }
        public LazyLoaded<List<Track>> Tracks { get; set; }

        public override string ToString()
        {
            return string.Format("[{0}][{1}]", ForeignReleaseId, Title.NullSafe());
        }

        public AlbumRelease ToAlbumRelease(DateTime? releaseDate)
        {
            return new AlbumRelease {
                Id = ForeignReleaseId,
                Title = Title,
                ReleaseDate = releaseDate,
                TrackCount = TrackCount,
                MediaCount = Media.Count,
                Disambiguation = Disambiguation,
                Country = Country,
                Format = string.Join(", ",
                                     Media.OrderBy(x => x.Number)
                                     .GroupBy(x => x.Format)
                                     .Select(g => MediaFormatHelper(g.Key, g.Count()))
                                     .ToList()),
                Label = Label
            };
        }

        private string MediaFormatHelper(string name, int count)
        {
            if (count == 1)
                return name;
            return string.Join("x", new List<string> {count.ToString(), name});
        }
    }
}
