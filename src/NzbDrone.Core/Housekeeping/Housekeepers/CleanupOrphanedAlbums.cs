using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedAlbums : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedAlbums(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            var mapper = _database.GetDataMapper();

            mapper.ExecuteNonQuery(@"DELETE FROM ReleaseGroup
                                     WHERE Id IN (
                                     SELECT ReleaseGroup.Id FROM ReleaseGroup
                                     LEFT OUTER JOIN Artist
                                     ON ReleaseGroup.ArtistMetadataId = Artist.ArtistMetadataId
                                     WHERE Artist.Id IS NULL)");
        }
    }
}
