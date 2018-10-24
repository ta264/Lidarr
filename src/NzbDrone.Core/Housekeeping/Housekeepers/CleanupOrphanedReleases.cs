using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedReleases : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedReleases(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            var mapper = _database.GetDataMapper();

            mapper.ExecuteNonQuery(@"DELETE FROM Release
                                     WHERE Id IN (
                                     SELECT Release.Id FROM Release
                                     LEFT OUTER JOIN ReleaseGroup
                                     ON Release.ReleaseGroupId = ReleaseGroup.Id
                                     WHERE ReleaseGroup.Id IS NULL)");
        }
    }
}
