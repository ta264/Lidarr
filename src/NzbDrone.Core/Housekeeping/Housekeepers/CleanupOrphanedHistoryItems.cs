using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedHistoryItems : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedHistoryItems(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            CleanupOrphanedByArtist();
            CleanupOrphanedByAlbum();
        }

        private void CleanupOrphanedByArtist()
        {
            var mapper = _database.GetDataMapper();

            mapper.ExecuteNonQuery(@"DELETE FROM History
                                     WHERE Id IN (
                                     SELECT History.Id FROM History
                                     LEFT OUTER JOIN Artist
                                     ON History.ArtistId = Artist.Id
                                     WHERE Artist.Id IS NULL)");
        }

        private void CleanupOrphanedByAlbum()
        {
            var mapper = _database.GetDataMapper();

            mapper.ExecuteNonQuery(@"DELETE FROM History
                                     WHERE Id IN (
                                     SELECT History.Id FROM History
                                     LEFT OUTER JOIN ReleaseGroup
                                     ON History.AlbumId = ReleaseGroup.Id
                                     WHERE ReleaseGroup.Id IS NULL)");
        }
    }
}
