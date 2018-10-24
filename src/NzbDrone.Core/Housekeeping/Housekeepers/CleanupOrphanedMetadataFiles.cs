using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedMetadataFiles : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedMetadataFiles(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            DeleteOrphanedByArtist();
            DeleteOrphanedByAlbum();
            DeleteOrphanedByTrackFile();
            DeleteWhereAlbumIdIsZero();
            DeleteWhereTrackFileIsZero();
        }

        private void DeleteOrphanedByArtist()
        {
            var mapper = _database.GetDataMapper();

            mapper.ExecuteNonQuery(@"DELETE FROM MetadataFiles
                                     WHERE Id IN (
                                     SELECT MetadataFiles.Id FROM MetadataFiles
                                     LEFT OUTER JOIN Artist
                                     ON MetadataFiles.ArtistId = Artist.Id
                                     WHERE Artist.Id IS NULL)");
        }

        private void DeleteOrphanedByAlbum()
        {
            var mapper = _database.GetDataMapper();

            mapper.ExecuteNonQuery(@"DELETE FROM MetadataFiles
                                     WHERE Id IN (
                                     SELECT MetadataFiles.Id FROM MetadataFiles
                                     LEFT OUTER JOIN ReleaseGroup
                                     ON MetadataFiles.AlbumId = ReleaseGroup.Id
                                     WHERE MetadataFiles.AlbumId > 0
                                     AND ReleaseGroup.Id IS NULL)");
        }

        private void DeleteOrphanedByTrackFile()
        {
            var mapper = _database.GetDataMapper();

            mapper.ExecuteNonQuery(@"DELETE FROM MetadataFiles
                                     WHERE Id IN (
                                     SELECT MetadataFiles.Id FROM MetadataFiles
                                     LEFT OUTER JOIN TrackFile
                                     ON MetadataFiles.TrackFileId = TrackFile.Id
                                     WHERE MetadataFiles.TrackFileId > 0
                                     AND TrackFile.Id IS NULL)");
        }

        private void DeleteWhereAlbumIdIsZero()
        {
            var mapper = _database.GetDataMapper();

            mapper.ExecuteNonQuery(@"DELETE FROM MetadataFiles
                                     WHERE Id IN (
                                     SELECT Id FROM MetadataFiles
                                     WHERE Type IN (4, 6)
                                     AND AlbumId = 0)");
        }

        private void DeleteWhereTrackFileIsZero()
        {
            var mapper = _database.GetDataMapper();

            mapper.ExecuteNonQuery(@"DELETE FROM MetadataFiles
                                     WHERE Id IN (
                                     SELECT Id FROM MetadataFiles
                                     WHERE Type IN (2, 5)
                                     AND TrackFileId = 0)");
        }
    }
}
