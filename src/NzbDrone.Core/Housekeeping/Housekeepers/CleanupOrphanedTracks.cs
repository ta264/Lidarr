using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedTracks : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedTracks(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            var mapper = _database.GetDataMapper();

            mapper.ExecuteNonQuery(@"DELETE FROM Track
                                     WHERE Id IN (
                                     SELECT Track.Id FROM Track
                                     LEFT OUTER JOIN Release
                                     ON Track.ReleaseId = Release.Id
                                     WHERE Release.Id IS NULL)");
        }
    }
}
