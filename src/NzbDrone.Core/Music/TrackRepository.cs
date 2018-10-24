using NzbDrone.Core.Datastore;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.MediaFiles;
using Marr.Data.QGen;
using NzbDrone.Core.Datastore.Extensions;
using System;

namespace NzbDrone.Core.Music
{
    public interface ITrackRepository : IBasicRepository<Track>
    {
        Track Find(int artistId, int releaseGroupId, int mediumNumber, int trackNumber);
        List<Track> GetTracks(int artistId);
        List<Track> GetTracksByAlbum(int releaseGroupId);
        List<Track> GetTracksByRelease(int releaseId);
        List<Track> GetTracksByForeignReleaseId(string foreignReleaseId);
        List<Track> GetTracksByMedium(int releaseGroupId, int mediumNumber);
        List<Track> GetTracksByFileId(int fileId);
        List<Track> TracksWithFiles(int artistId);
        void SetFileId(int trackId, int fileId);
    }

    public class TrackRepository : BasicRepository<Track>, ITrackRepository
    {
        private readonly IMainDatabase _database;
        private readonly Logger _logger;

        public TrackRepository(IMainDatabase database, IEventAggregator eventAggregator, Logger logger)
            : base(database, eventAggregator)
        {
            _database = database;
            _logger = logger;
        }

        public Track Find(int artistId, int releaseGroupId, int mediumNumber, int trackNumber)
        {
            string query = string.Format("SELECT Track.* " +
                                         "FROM Artist " +
                                         "JOIN ReleaseGroup ON ReleaseGroup.ArtistMetadataId == Artist.ArtistMetadataId " +
                                         "JOIN Track ON Track.ReleaseId == ReleaseGroup.SelectedReleaseId " +
                                         "WHERE Artist.Id = {0} " +
                                         "AND ReleaseGroup.Id = {1} " +
                                         "AND Track.MediumNumber = {2} " +
                                         "AND Track.AbsoluteTrackNumber = {3}",
                                         artistId, releaseGroupId, mediumNumber, trackNumber);

            return Query.QueryText(query).SingleOrDefault();
        }


        public List<Track> GetTracks(int artistId)
        {
            string query = string.Format("SELECT Track.* " +
                                         "FROM Artist " +
                                         "JOIN ReleaseGroup ON ReleaseGroup.ArtistMetadataId == Artist.ArtistMetadataId " +
                                         "JOIN Track ON Track.ReleaseId == ReleaseGroup.SelectedReleaseId " +
                                         "WHERE Artist.Id = {0}",
                                         artistId);
            
            return Query.QueryText(query).ToList();
        }

        public List<Track> GetTracksByAlbum(int releaseGroupId)
        {
            string query = string.Format("SELECT Track.* " +
                                         "FROM ReleaseGroup " +
                                         "JOIN Track ON Track.ReleaseId == ReleaseGroup.SelectedReleaseId " +
                                         "WHERE ReleaseGroup.Id = {0}",
                                         releaseGroupId);

            return Query.QueryText(query).ToList();
        }

        public List<Track> GetTracksByRelease(int releaseId)
        {
            return Query.Where(t => t.ReleaseId == releaseId).ToList();
        }

        public List<Track> GetTracksByForeignReleaseId(string foreignReleaseId)
        {
            string query = string.Format("SELECT Track.* " +
                                         "FROM Release " +
                                         "JOIN Track ON Track.ReleaseId == Release.Id " +
                                         "WHERE Release.ForeignReleaseId = '{0}'",
                                         foreignReleaseId);

            return Query.QueryText(query).ToList();
        }

        public List<Track> GetTracksByMedium(int releaseGroupId, int mediumNumber)
        {
            string query = string.Format("SELECT Track.* " +
                                         "FROM ReleaseGroup " +
                                         "JOIN Track ON Track.ReleaseId == ReleaseGroup.SelectedReleaseId " +
                                         "WHERE ReleaseGroup.Id = {0} " +
                                         "AND Track.MediumNumber = {1}",
                                         releaseGroupId,
                                         mediumNumber);

            return Query.QueryText(query).ToList();
        }

        public List<Track> GetTracksByFileId(int fileId)
        {
            return Query.Where(e => e.TrackFileId == fileId).ToList();
        }

        public List<Track> TracksWithFiles(int artistId)
        {
            string query = string.Format("SELECT Track.* " +
                                         "FROM Artist " +
                                         "JOIN ReleaseGroup ON ReleaseGroup.ArtistMetadataId = Artist.ArtistMetadataId " +
                                         "JOIN Track ON Track.ReleaseId == ReleaseGroup.SelectedReleaseId " +
                                         "JOIN TrackFile ON TrackFile.Id == Track.TrackFileId " +
                                         "WHERE Artist.Id == {0}",
                                         artistId);

            return Query.QueryText(query).ToList();
        }

        public void SetFileId(int trackId, int fileId)
        {
            SetFields(new Track { Id = trackId, TrackFileId = fileId }, track => track.TrackFileId);
        }
    }
}
