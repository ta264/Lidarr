using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using System;
using System.Linq;
using System.Collections.Generic;
using Marr.Data;

namespace NzbDrone.Core.Music
{
    public class Album : ModelBase
    {
        public Album()
        {
            Genres = new List<string>();
            Images = new List<MediaCover.MediaCover>();
            Ratings = new Ratings();
            SelectedRelease = new Release();
            Artist = new Artist();
        }

        public const string RELEASE_DATE_FORMAT = "yyyy-MM-dd";

        // These correspond to columns in the ReleaseGroups table
        // These are metadata entries
        public int ArtistMetadataId { get; set; }
        public string ForeignReleaseGroupId { get; set; }
        public string Title { get; set; }
        public string Disambiguation { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public List<MediaCover.MediaCover> Images { get; set; }
        public List<string> Genres { get; set; }
        public String AlbumType { get; set; }
        public List<SecondaryAlbumType> SecondaryTypes { get; set; }
        public Ratings Ratings { get; set; }

        // These are Lidarr generated/config        
        public string CleanTitle { get; set; }
        public int ProfileId { get; set; }
        public bool Monitored { get; set; }
        public DateTime? LastInfoSync { get; set; }
        public DateTime Added { get; set; }
        public AddArtistOptions AddOptions { get; set; }
        public int SelectedReleaseId { get; set; }

        // These are dynamically queried from other tables
        public LazyLoaded<Release> SelectedRelease { get; set; }
        public LazyLoaded<ArtistMetadata> ArtistMetadata { get; set; }
        public LazyLoaded<List<Release>> Releases { get; set; }
        public LazyLoaded<Artist> Artist { get; set; }

        //compatibility properties with old version of Album
        public string ForeignAlbumId { get { return ForeignReleaseGroupId; } set { ForeignReleaseGroupId = value; } }
        public int ArtistId { get { return Artist?.Value?.Id ?? 0; } set { Artist.Value.Id = value; } }
        public List<string> Label { get { return SelectedRelease?.Value?.Label; } }
        public int Duration { get { return SelectedRelease?.Value?.Duration ?? 0; } }
        public List<Track> Tracks { get; set; }
        public List<Medium> Media { get { return SelectedRelease?.Value?.Media; } }
        public List<AlbumRelease> AlbumReleases { get { return Releases?.Value?.Select(x => x.ToAlbumRelease(ReleaseDate))?.ToList(); } }
        public AlbumRelease CurrentAlbumRelease { get { return SelectedRelease?.Value?.ToAlbumRelease(ReleaseDate);} }

        public override string ToString()
        {
            return string.Format("[{0}][{1}]", ForeignAlbumId, Title.NullSafe());
        }

        public void ApplyChanges(Album otherAlbum)
        {

            ForeignReleaseGroupId = otherAlbum.ForeignAlbumId;

            Tracks = otherAlbum.Tracks;

            ProfileId = otherAlbum.ProfileId;
            AddOptions = otherAlbum.AddOptions;
            Monitored = otherAlbum.Monitored;
            SelectedReleaseId = otherAlbum.SelectedReleaseId;

        }

    }
}
