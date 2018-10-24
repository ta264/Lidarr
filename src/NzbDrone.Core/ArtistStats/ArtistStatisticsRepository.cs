using System;
using System.Collections.Generic;
using System.Text;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.ArtistStats
{
    public interface IArtistStatisticsRepository
    {
        List<AlbumStatistics> ArtistStatistics();
        List<AlbumStatistics> ArtistStatistics(int artistId);
    }

    public class ArtistStatisticsRepository : IArtistStatisticsRepository
    {
        private readonly IMainDatabase _database;

        public ArtistStatisticsRepository(IMainDatabase database)
        {
            _database = database;
        }

        public List<AlbumStatistics> ArtistStatistics()
        {
            var mapper = _database.GetDataMapper();

            mapper.AddParameter("currentDate", DateTime.UtcNow);

            var sb = new StringBuilder();
            sb.AppendLine(GetSelectClause());
            sb.AppendLine("WHERE ReleaseGroup.ReleaseDate < @currentDate");
            sb.AppendLine(GetGroupByClause());
            var queryText = sb.ToString();

            return mapper.Query<AlbumStatistics>(queryText);
        }

        public List<AlbumStatistics> ArtistStatistics(int artistId)
        {
            var mapper = _database.GetDataMapper();

            mapper.AddParameter("currentDate", DateTime.UtcNow);
            mapper.AddParameter("artistId", artistId);

            var sb = new StringBuilder();
            sb.AppendLine(GetSelectClause());
            sb.AppendLine("WHERE Artist.Id = @artistId");
            sb.AppendLine("AND ReleaseGroup.ReleaseDate < @currentDate");
            sb.AppendLine(GetGroupByClause());
            var queryText = sb.ToString();

            return mapper.Query<AlbumStatistics>(queryText);
        }

        private string GetSelectClause()
        {
            return @"SELECT
                     Artist.Id AS ArtistId,
                     ReleaseGroup.Id AS AlbumId,
                     SUM(COALESCE(TrackFile.Size, 0)) AS SizeOnDisk,
                     COUNT(Track.Id) AS TotalTrackCount,
                     SUM(CASE WHEN Track.TrackFileId > 0 THEN 1 ELSE 0 END) AS AvailableTrackCount,
                     SUM(CASE WHEN ReleaseGroup.Monitored = 1 OR Track.TrackFileId > 0 THEN 1 ELSE 0 END) AS TrackCount,
                     SUM(CASE WHEN TrackFile.Id IS NULL THEN 0 ELSE 1 END) AS TrackFileCount
                     FROM Track
                     JOIN ReleaseGroup ON Track.ReleaseId = ReleaseGroup.SelectedReleaseId
                     JOIN Artist on ReleaseGroup.ArtistMetadataId = Artist.ArtistMetadataId
                     LEFT OUTER JOIN TrackFile ON Track.TrackFileId = TrackFile.Id";
        }

        private string GetGroupByClause()
        {
            return "GROUP BY Artist.Id, ReleaseGroup.Id";
        }
    }
}
