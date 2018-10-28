using System;
using System.Linq;
using NLog;
using Marr.Data.QGen;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.Extensions;
using System.Collections.Generic;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Qualities;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Music
{
    public interface IAlbumRepository : IBasicRepository<Album>
    {
        List<Album> GetAlbums(int artistId);
        Album FindByName(string cleanTitle);
        Album FindByTitle(int artistMetadataId, string title);
        Album FindByTitleInexact(int artistMetadataId, string title);
        Album FindByArtistAndName(string artistName, string cleanTitle);
        Album FindById(string spotifyId);
        PagingSpec<Album> AlbumsWithoutFiles(PagingSpec<Album> pagingSpec);
        PagingSpec<Album> AlbumsWhereCutoffUnmet(PagingSpec<Album> pagingSpec, List<QualitiesBelowCutoff> qualitiesBelowCutoff, List<LanguagesBelowCutoff> languagesBelowCutoff);
        List<Album> AlbumsBetweenDates(DateTime startDate, DateTime endDate, bool includeUnmonitored);
        List<Album> ArtistAlbumsBetweenDates(Artist artist, DateTime startDate, DateTime endDate, bool includeUnmonitored);
        void SetMonitoredFlat(Album album, bool monitored);
        void SetMonitored(IEnumerable<int> ids, bool monitored);
        Album FindAlbumByRelease(string releaseId);
        Album FindAlbumByTrack(int trackId);
        List<Album> GetArtistAlbumsWithFiles(Artist artist);
    }

    public class AlbumRepository : BasicRepository<Album>, IAlbumRepository
    {
        private readonly IMainDatabase _database;
        private readonly Logger _logger;

        public AlbumRepository(IMainDatabase database, IEventAggregator eventAggregator, Logger logger)
            : base(database, eventAggregator)
        {
            _database = database;
            _logger = logger;
        }

        public List<Album> GetAlbums(int artistId)
        {
            return Query.Join<Album, Artist>(JoinType.Inner, album => album.Artist, (l, r) => l.ArtistMetadataId == r.ArtistMetadataId)
                .Where<Artist>(a => a.Id == artistId).ToList();
        }

        public Album FindById(string foreignReleaseGroupId)
        {
            return Query.Where(s => s.ForeignReleaseGroupId == foreignReleaseGroupId).SingleOrDefault();
        }

        public PagingSpec<Album> AlbumsWithoutFiles(PagingSpec<Album> pagingSpec)
        {
            var currentTime = DateTime.UtcNow;

            //pagingSpec.TotalRecords = GetMissingAlbumsQuery(pagingSpec, currentTime).GetRowCount(); Cant Use GetRowCount with a Manual Query

            pagingSpec.TotalRecords = GetMissingAlbumsQueryCount(pagingSpec, currentTime);
            pagingSpec.Records = GetMissingAlbumsQuery(pagingSpec, currentTime).ToList();

            return pagingSpec;
        }

        public PagingSpec<Album> AlbumsWhereCutoffUnmet(PagingSpec<Album> pagingSpec, List<QualitiesBelowCutoff> qualitiesBelowCutoff, List<LanguagesBelowCutoff> languagesBelowCutoff)
        {

            pagingSpec.TotalRecords = GetCutOffAlbumsQueryCount(pagingSpec, qualitiesBelowCutoff, languagesBelowCutoff);
            pagingSpec.Records = GetCutOffAlbumsQuery(pagingSpec, qualitiesBelowCutoff, languagesBelowCutoff).ToList();

            return pagingSpec;
        }

        public List<Album> AlbumsBetweenDates(DateTime startDate, DateTime endDate, bool includeUnmonitored)
        {
            var query = Query.Join<Album, Artist>(JoinType.Inner, rg => rg.Artist, (rg, a) => rg.ArtistMetadataId == a.ArtistMetadataId)
                             .Where<Album>(rg => rg.ReleaseDate >= startDate)
                             .AndWhere(rg => rg.ReleaseDate <= endDate);


            if (!includeUnmonitored)
            {
                query.AndWhere(e => e.Monitored)
                     .AndWhere(e => e.Artist.Value.Monitored);
            }

            return query.ToList();
        }

        public List<Album> ArtistAlbumsBetweenDates(Artist artist, DateTime startDate, DateTime endDate, bool includeUnmonitored)
        {
            var query = Query.Join<Album, Artist>(JoinType.Inner, e => e.Artist, (e, s) => e.ArtistMetadataId == s.ArtistMetadataId)
                .Where<Album>(e => e.ReleaseDate >= startDate)
                .AndWhere(e => e.ReleaseDate <= endDate)
                .AndWhere(e => e.ArtistMetadataId == artist.ArtistMetadataId);


            if (!includeUnmonitored)
            {
                query.AndWhere(e => e.Monitored)
                    .AndWhere(e => e.Artist.Value.Monitored);
            }

            return query.ToList();
        }

        private QueryBuilder<Album> GetMissingAlbumsQuery(PagingSpec<Album> pagingSpec, DateTime currentTime)
        {
            string sortKey;
            string monitored = "(ReleaseGroup.[Monitored] = 0) OR (Artist.[Monitored] = 0)";

            if (pagingSpec.FilterExpressions.FirstOrDefault().ToString().Contains("True"))
            {
                monitored = "(ReleaseGroup.[Monitored] = 1) AND (Artist.[Monitored] = 1)";
            }

            if (pagingSpec.SortKey == "releaseDate")
            {
                sortKey = "ReleaseGroup." + pagingSpec.SortKey;
            }
            else if (pagingSpec.SortKey == "artist.sortName")
            {
                sortKey = "Artist." + pagingSpec.SortKey.Split('.').Last();
            }
            else if (pagingSpec.SortKey == "albumTitle")
            {
                sortKey = "ReleaseGroup.title";
            }
            else
            {
                sortKey = "ReleaseGroup.releaseDate";
            }

            string query = string.Format("SELECT ReleaseGroup.* " +
                                         "FROM ReleaseGroup " +
                                         "JOIN Artist ON ReleaseGroup.ArtistMetadataId = Artist.ArtistMetadataId " +
                                         "JOIN Track ON Track.ReleaseId == ReleaseGroup.SelectedReleaseId " +
                                         "LEFT OUTER JOIN TrackFile ON TrackFile.Id == Track.TrackFileId " +
                                         "WHERE TrackFile.Id IS NULL " +
                                         "AND ({0}) AND {1} " +
                                         "GROUP BY ReleaseGroup.Id " +
                                         " ORDER BY {2} {3} LIMIT {4} OFFSET {5}",
                                         monitored,
                                         BuildReleaseDateCutoffWhereClause(currentTime),
                                         sortKey,
                                         pagingSpec.ToSortDirection(),
                                         pagingSpec.PageSize,
                                         pagingSpec.PagingOffset());

            return Query.QueryText(query);
        }

        private int GetMissingAlbumsQueryCount(PagingSpec<Album> pagingSpec, DateTime currentTime)
        {
            var monitored = "(ReleaseGroup.[Monitored] = 0) OR (Artist.[Monitored] = 0)";

            if (pagingSpec.FilterExpressions.FirstOrDefault().ToString().Contains("True"))
            {
                monitored = "(ReleaseGroup.[Monitored] = 1) AND (Artist.[Monitored] = 1)";
            }

            string query = string.Format("SELECT ReleaseGroup.* " +
                                         "FROM ReleaseGroup " +
                                         "JOIN Artist ON ReleaseGroup.ArtistMetadataId = Artist.ArtistMetadataId " +
                                         "JOIN Track ON Track.ReleaseId == ReleaseGroup.SelectedReleaseId " +
                                         "LEFT OUTER JOIN TrackFile ON TrackFile.Id == Track.TrackFileId " +
                                         "WHERE TrackFile.Id IS NULL " +
                                         "AND ({0}) AND {1} " +
                                         "GROUP BY ReleaseGroup.Id ",
                                         monitored,
                                         BuildReleaseDateCutoffWhereClause(currentTime));

            return Query.QueryText(query).Count();
        }

        private string BuildReleaseDateCutoffWhereClause(DateTime currentTime)
        {
            return string.Format("datetime(strftime('%s', ReleaseGroup.[ReleaseDate]),  'unixepoch') <= '{0}'",
                                 currentTime.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        private QueryBuilder<Album> GetCutOffAlbumsQuery(PagingSpec<Album> pagingSpec, List<QualitiesBelowCutoff> qualitiesBelowCutoff, List<LanguagesBelowCutoff> languagesBelowCutoff)
        {
            string sortKey;
            string monitored = "(ReleaseGroup.[Monitored] = 0) OR (Artist.[Monitored] = 0)";

            if (pagingSpec.FilterExpressions.FirstOrDefault().ToString().Contains("True"))
            {
                monitored = "(ReleaseGroup.[Monitored] = 1) AND (Artist.[Monitored] = 1)";
            }

            if (pagingSpec.SortKey == "releaseDate")
            {
                sortKey = "ReleaseGroup." + pagingSpec.SortKey;
            }
            else if (pagingSpec.SortKey == "artist.sortName")
            {
                sortKey = "Artist." + pagingSpec.SortKey.Split('.').Last();
            }
            else if (pagingSpec.SortKey == "albumTitle")
            {
                sortKey = "ReleaseGroup.title";
            }
            else
            {
                sortKey = "ReleaseGroup.releaseDate";
            }

            string query = string.Format("SELECT ReleaseGroup.* " +
                                         "FROM ReleaseGroup " +
                                         "JOIN Artist on ReleaseGroup.ArtistMetadataId == Artist.ArtistMetadataId " +
                                         "JOIN Track ON Track.ReleaseId == ReleaseGroup.SelectedReleaseId " +
                                         "JOIN TrackFile ON TrackFile.Id == Track.TrackFileId " +
                                         "WHERE {0} " +
                                         "GROUP BY ReleaseGroup.Id " +
                                         "HAVING ({1} OR {2}) " +
                                         "ORDER BY {3} {4} LIMIT {5} OFFSET {6}",
                                         monitored,
                                         BuildQualityCutoffWhereClause(qualitiesBelowCutoff),
                                         BuildLanguageCutoffWhereClause(languagesBelowCutoff),
                                         sortKey,
                                         pagingSpec.ToSortDirection(),
                                         pagingSpec.PageSize,
                                         pagingSpec.PagingOffset());

            return Query.QueryText(query);

        }

        private int GetCutOffAlbumsQueryCount(PagingSpec<Album> pagingSpec, List<QualitiesBelowCutoff> qualitiesBelowCutoff, List<LanguagesBelowCutoff> languagesBelowCutoff)
        {
            var monitored = "(ReleaseGroup.[Monitored] = 0) OR (Artist.[Monitored] = 0)";

            if (pagingSpec.FilterExpressions.FirstOrDefault().ToString().Contains("True"))
            {
                monitored = "(ReleaseGroup.[Monitored] = 1) AND (Artist.[Monitored] = 1)";
            }

            string query = string.Format("SELECT ReleaseGroup.* " +
                                         "FROM ReleaseGroup " +
                                         "JOIN Artist on ReleaseGroup.ArtistMetadataId == Artist.ArtistMetadataId " +
                                         "JOIN Track ON Track.ReleaseId == ReleaseGroup.SelectedReleaseId " +
                                         "JOIN TrackFile ON TrackFile.Id == Track.TrackFileId " +
                                         "WHERE {0} " +
                                         "GROUP BY ReleaseGroup.Id " +
                                         "HAVING ({1} OR {2}) ",
                                         monitored,
                                         BuildQualityCutoffWhereClause(qualitiesBelowCutoff),
                                         BuildLanguageCutoffWhereClause(languagesBelowCutoff));

            return Query.QueryText(query).Count();
        }


        private string BuildLanguageCutoffWhereClause(List<LanguagesBelowCutoff> languagesBelowCutoff)
        {
            var clauses = new List<string>();

            foreach (var language in languagesBelowCutoff)
            {
                foreach (var belowCutoff in language.LanguageIds)
                {
                    clauses.Add(string.Format("(Artist.[LanguageProfileId] = {0} AND TrackFile.[Language] = {1})", language.ProfileId, belowCutoff));
                }
            }

            return string.Format("({0})", string.Join(" OR ", clauses));
        }

        private string BuildQualityCutoffWhereClause(List<QualitiesBelowCutoff> qualitiesBelowCutoff)
        {
            var clauses = new List<string>();

            foreach (var profile in qualitiesBelowCutoff)
            {
                foreach (var belowCutoff in profile.QualityIds)
                {
                    clauses.Add(string.Format("(Artist.[ProfileId] = {0} AND MIN(TrackFile.Quality) LIKE '%_quality_: {1},%')", profile.ProfileId, belowCutoff));
                }
            }

            return string.Format("({0})", string.Join(" OR ", clauses));
        }

        public void SetMonitoredFlat(Album album, bool monitored)
        {
            album.Monitored = monitored;
            SetFields(album, p => p.Monitored);
        }

        public void SetMonitored(IEnumerable<int> ids, bool monitored)
        {
            var mapper = _database.GetDataMapper();

            mapper.AddParameter("monitored", monitored);

            var sql = "UPDATE ReleaseGroup " +
                      "SET Monitored = @monitored " +
                      $"WHERE Id IN ({string.Join(", ", ids)})";

            mapper.ExecuteNonQuery(sql);
        }

        public Album FindByName(string cleanTitle)
        {
            cleanTitle = cleanTitle.ToLowerInvariant();

            return Query.Where(s => s.CleanTitle == cleanTitle).SingleOrDefault();
        }

        public Album FindByTitle(int artistMetadataId, string title)
        {
            var cleanTitle = Parser.Parser.CleanArtistName(title);
            
            if (string.IsNullOrEmpty(cleanTitle))
                cleanTitle = title;
            
            return Query.Where(s => s.CleanTitle == cleanTitle || s.Title == title)
                        .AndWhere(s => s.ArtistMetadataId == artistMetadataId)
                        .FirstOrDefault();
        }

        public Album FindByTitleInexact(int artistMetadataId, string title)
        {
            double fuzzThreshold = 0.7;
            double fuzzGap = 0.4;
            
            var album = FindByTitleInexact(artistMetadataId, title, fuzzThreshold, fuzzGap);

            if (album == null)
            {
                var titleNoDisambiguation = Parser.RemoveDisambiguation(title);
                album = FindByTitleInexact(artistMetadataId, titleNoDisambiguation, fuzzThreshold, fuzzGap);
            }
            
            if (album == null)
            {
                var titleNoBrackets = Parser.RemoveBracketAndContents(titleNoDisambiguation);
                album = FindByTitleInexact(artistMetadataId, titleNoBrackets, fuzzThreshold, fuzzGap);
            }

        }

        private Album FindByTitleInexact(int artistMetadataId, string title, double threshold, double gap)
        {
            var cleanTitle = Parser.Parser.CleanArtistName(title);

            if (string.IsNullOrEmpty(cleanTitle))
                cleanTitle = title;

            var sortedAlbums = Query.Where(s => s.ArtistMetadataId == artistMetadataId)
                .Select(s => new
                    {
                        MatchProb = s.CleanTitle.FuzzyMatch(cleanTitle),
                        Album = s
                    })
                .ToList()
                .OrderByDescending(s => s.MatchProb)
                .ToList();

            if (!sortedAlbums.Any())
                return null;

            _logger.Trace("\nFuzzy album match on '{0}':\n{1}",
                          cleanTitle,
                          string.Join("\n", sortedAlbums.Select(x => $"{x.Album.CleanTitle}: {x.MatchProb}")));

            if (sortedAlbums[0].MatchProb > threshold
                && (sortedAlbums.Count == 1 || sortedAlbums[0].MatchProb - sortedAlbums[1].MatchProb > gap))
                return sortedAlbums[0].Album;

            return null;
        }

        public Album FindByArtistAndName(string artistName, string cleanTitle)
        {
            var cleanArtistName = Parser.Parser.CleanArtistName(artistName);
            cleanTitle = cleanTitle.ToLowerInvariant();

            return Query.Join<Album, Artist>(JoinType.Inner, rg => rg.Artist, (rg, artist) => rg.ArtistMetadataId == artist.ArtistMetadataId)
                        .Where<Artist>(artist => artist.CleanName == cleanArtistName)
                        .SingleOrDefault(rg => rg.CleanTitle == cleanTitle);
        }

        public Album FindAlbumByRelease(string releaseId)
        {
            string query = string.Format("SELECT ReleaseGroup.* " +
                                         "FROM ReleaseGroup " +
                                         "JOIN Release ON Release.ReleaseGroupId = ReleaseGroup.Id " +
                                         "WHERE Release.ForeignReleaseId = '{0}'",
                                         releaseId);
            return Query.QueryText(query).FirstOrDefault();
        }

        public Album FindAlbumByTrack(int trackId)
        {
            string query = string.Format("SELECT ReleaseGroup.* " +
                                         "FROM ReleaseGroup " +
                                         "JOIN Release ON Release.ReleaseGroupId = ReleaseGroup.Id " +
                                         "JOIN Track ON Track.ReleaseId = Release.Id " +
                                         "WHERE Track.Id = {0}",
                                         trackId);
            return Query.QueryText(query).FirstOrDefault();
        }

        public List<Album> GetArtistAlbumsWithFiles(Artist artist)
        {
            string query = string.Format("SELECT ReleaseGroup.* " +
                                         "FROM ReleaseGroup " +
                                         "JOIN Track ON Track.ReleaseId == ReleaseGroup.SelectedReleaseId " +
                                         "JOIN TrackFile ON TrackFile.Id == Track.TrackFileId " +
                                         "WHERE ReleaseGroup.ArtistMetadataId == {0} " +
                                         "GROUP BY ReleaseGroup.Id ",
                                         artist.ArtistMetadataId);

            return Query.QueryText(query).ToList();
        }
    }
}
