using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedPendingReleases : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedPendingReleases(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            var mapper = _database.GetDataMapper();

            mapper.ExecuteNonQuery(@"DELETE FROM PendingReleases
                                     WHERE Id IN (
                                     SELECT PendingReleases.Id FROM PendingReleases
                                     LEFT OUTER JOIN Artist
                                     ON PendingReleases.ArtistId = Artist.Id
                                     WHERE Artist.Id IS NULL)");
        }
    }
}
