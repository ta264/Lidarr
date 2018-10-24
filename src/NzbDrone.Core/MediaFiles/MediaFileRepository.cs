using System;
using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;


namespace NzbDrone.Core.MediaFiles
{
    public interface IMediaFileRepository : IBasicRepository<TrackFile>
    {
        List<TrackFile> GetFilesByArtist(int artistId);
        List<TrackFile> GetFilesByAlbum(int albumId);
        List<TrackFile> GetFilesWithoutMediaInfo();
        List<TrackFile> GetFilesWithRelativePath(int artistId, string relativePath);
    }


    public class MediaFileRepository : BasicRepository<TrackFile>, IMediaFileRepository
    {
        public MediaFileRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<TrackFile> GetFilesWithoutMediaInfo()
        {
            return Query.Where(c => c.MediaInfo == null).ToList();
        }

        public List<TrackFile> GetFilesByArtist(int artistId)
        {
            string query = string.Format("SELECT TrackFile.* " +
                                         "FROM Artist " +
                                         "JOIN ReleaseGroup ON ReleaseGroup.ArtistMetadataId = Artist.ArtistMetadataId " +
                                         "JOIN Track ON Track.ReleaseId == ReleaseGroup.SelectedReleaseId " +
                                         "JOIN TrackFile ON TrackFile.Id == Track.TrackFileId " +
                                         "WHERE Artist.Id == {0}",
                                         artistId);

            return Query.QueryText(query).ToList();
        }

        public List<TrackFile> GetFilesByAlbum(int albumId)
        {
            string query = string.Format("SELECT TrackFile.* " +
                                         "FROM ReleaseGroup " +
                                         "JOIN Track ON Track.ReleaseId == ReleaseGroup.SelectedReleaseId " +
                                         "JOIN TrackFile ON TrackFile.Id == Track.TrackFileId " +
                                         "WHERE ReleaseGroup.Id == {0}",
                                         albumId);

            return Query.QueryText(query).ToList();
        }
        
        public List<TrackFile> GetFilesWithRelativePath(int artistId, string relativePath)
        {
            string query = string.Format("SELECT TrackFile.* " +
                                         "FROM Artist " +
                                         "JOIN ReleaseGroup ON ReleaseGroup.ArtistMetadataId = Artist.ArtistMetadataId " +
                                         "JOIN Track ON Track.ReleaseId == ReleaseGroup.SelectedReleaseId " +
                                         "JOIN TrackFile ON TrackFile.Id == Track.TrackFileId " +
                                         "WHERE Artist.Id == {0} " +
                                         "AND TrackFile.RelativePath == '{1}'",
                                         artistId, relativePath);

            return Query.QueryText(query).ToList();
        }

    }
}
