using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedTrackFiles : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedTrackFiles(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            var mapper = _database.GetDataMapper();

            // Delete where track no longer exists
            mapper.ExecuteNonQuery(@"DELETE FROM TrackFile
                                     WHERE Id IN (
                                     SELECT TrackFile.Id FROM TrackFile
                                     LEFT OUTER JOIN Track
                                     ON TrackFile.Id = Track.TrackFileId
                                     WHERE Track.Id IS NULL)");

            // Delete trackfiles associated with releases that are not currently selected
            mapper.ExecuteNonQuery(@"DELETE FROM TrackFile
                                     WHERE Id IN (
                                     SELECT TrackFile.Id FROM TrackFile
                                     JOIN Track ON TrackFile.Id = Track.TrackFileId
                                     JOIN Release ON Track.ReleaseId = Release.Id
                                     JOIN ReleaseGroup ON Release.ReleaseGroupId = ReleaseGroup.Id
                                     WHERE Release.Id != ReleaseGroup.SelectedReleaseId)");

        }
    }
}
