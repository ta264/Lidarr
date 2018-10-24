using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Music.Events;
using NzbDrone.Common.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NzbDrone.Core.Music
{
    public interface ITrackService
    {
        Track GetTrack(int id);
        List<Track> GetTracks(IEnumerable<int> ids);
        Track FindTrack(int artistId, int albumId, int mediumNumber, int trackNumber);
        Track FindTrackByTitle(int artistId, int albumId, int mediumNumber, int trackNumber, string releaseTitle);
        Track FindTrackByTitleInexact(int artistId, int albumId, int mediumNumber, int trackNumber, string releaseTitle);
        List<Track> GetTracksByArtist(int artistId);
        List<Track> GetTracksByAlbum(int albumId);
        List<Track> GetTracksByRelease(int releaseId);
        List<Track> GetTracksByForeignReleaseId(string foreignReleaseId);
        List<Track> TracksWithFiles(int artistId);
        List<Track> GetTracksByFileId(int trackFileId);
        void UpdateTrack(Track track);
        void UpdateTracks(List<Track> tracks);
        void InsertMany(List<Track> tracks);
        void UpdateMany(List<Track> tracks);
        void DeleteMany(List<Track> tracks);
        // void SetTrackMonitoredByAlbum(int artistId, int albumId, bool monitored);
    }

    public class TrackService : ITrackService,
                                IHandleAsync<ArtistDeletedEvent>,
                                IHandleAsync<AlbumDeletedEvent>,
                                IHandle<TrackFileDeletedEvent>,
                                IHandle<TrackFileAddedEvent>
    {
        private readonly ITrackRepository _trackRepository;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public TrackService(ITrackRepository trackRepository, IConfigService configService, Logger logger)
        {
            _trackRepository = trackRepository;
            _configService = configService;
            _logger = logger;
        }

        public Track GetTrack(int id)
        {
            return _trackRepository.Get(id);
        }

        public List<Track> GetTracks(IEnumerable<int> ids)
        {
            return _trackRepository.Get(ids).ToList();
        }

        public Track FindTrack(int artistId, int albumId, int mediumNumber, int trackNumber)
        {
            return _trackRepository.Find(artistId, albumId, mediumNumber, trackNumber);
        }

        public List<Track> GetTracksByArtist(int artistId)
        {
            _logger.Debug("Getting Tracks for ArtistId {0}", artistId);
            return _trackRepository.GetTracks(artistId).ToList();
        }

        public List<Track> GetTracksByAlbum(int albumId)
        {
            return _trackRepository.GetTracksByAlbum(albumId);
        }

        public List<Track> GetTracksByRelease(int releaseId)
        {
            return _trackRepository.GetTracksByRelease(releaseId);
        }

        public List<Track> GetTracksByForeignReleaseId(string foreignReleaseId)
        {
            return _trackRepository.GetTracksByForeignReleaseId(foreignReleaseId);
        }

        public Track FindTrackByTitle(int artistId, int albumId, int mediumNumber, int trackNumber, string releaseTitle)
        {
            // TODO: can replace this search mechanism with something smarter/faster/better
            var normalizedReleaseTitle = Parser.Parser.NormalizeTrackTitle(releaseTitle).Replace(".", " ");
            var tracks = _trackRepository.GetTracksByMedium(albumId, mediumNumber);

            var matches = from track in tracks
                //if we have a trackNumber use it
                let trackNumCheck = (trackNumber == 0 || track.AbsoluteTrackNumber == trackNumber)
                //if release title is longer than track title
                let posReleaseTitle = normalizedReleaseTitle.IndexOf(Parser.Parser.NormalizeTrackTitle(track.Title), StringComparison.CurrentCultureIgnoreCase)
                //if track title is longer than release title 
                let posTrackTitle = Parser.Parser.NormalizeTrackTitle(track.Title).IndexOf(normalizedReleaseTitle, StringComparison.CurrentCultureIgnoreCase)
                where track.Title.Length > 0 && trackNumCheck && (posReleaseTitle >= 0 || posTrackTitle >= 0)
                orderby posReleaseTitle, posTrackTitle
                select new
                {
                    NormalizedLength = Parser.Parser.NormalizeTrackTitle(track.Title).Length,
                    Track = track
                };

            return matches.OrderByDescending(e => e.NormalizedLength).FirstOrDefault()?.Track;
        }

        public Track FindTrackByTitleInexact(int artistId, int albumId, int mediumNumber, int trackNumber, string releaseTitle)
        {
            double fuzzThreshold = 0.6;
            double fuzzGap = 0.2;

            var normalizedReleaseTitle = Parser.Parser.NormalizeTrackTitle(releaseTitle).Replace(".", " ");
            var tracks = _trackRepository.GetTracksByMedium(albumId, mediumNumber);

            var matches = from track in tracks
                let normalizedTitle = Parser.Parser.NormalizeTrackTitle(track.Title).Replace(".", " ")
                let matchProb = normalizedTitle.FuzzyMatch(normalizedReleaseTitle)
                where track.Title.Length > 0
                orderby matchProb descending
                select new
                {
                    MatchProb = matchProb,
                    NormalizedTitle = normalizedTitle,
                    Track = track
                };

            var matchList = matches.ToList();

            if (!matchList.Any())
                return null;

            _logger.Trace("\nFuzzy track match on '{0}':\n{1}",
                          normalizedReleaseTitle,
                          string.Join("\n", matchList.Select(x => $"{x.NormalizedTitle}: {x.MatchProb}")));

            if (matchList[0].MatchProb > fuzzThreshold
                && (matchList.Count == 1 || matchList[0].MatchProb - matchList[1].MatchProb > fuzzGap)
                && (trackNumber == 0 || matchList[0].Track.AbsoluteTrackNumber == trackNumber))
                return matchList[0].Track;

            return null;
        }

        public List<Track> TracksWithFiles(int artistId)
        {
            return _trackRepository.TracksWithFiles(artistId);
        }

        public List<Track> GetTracksByFileId(int trackFileId)
        {
            return _trackRepository.GetTracksByFileId(trackFileId);
        }

        public void UpdateTrack(Track track)
        {
            _trackRepository.Update(track);
        }

        // public void SetTrackMonitoredByAlbum(int artistId, int albumId, bool monitored)
        // {
        //     _trackRepository.SetMonitoredByAlbum(artistId, albumId, monitored);
        // }

        public void UpdateTracks(List<Track> tracks)
        {
            _trackRepository.UpdateMany(tracks);
        }

        public void InsertMany(List<Track> tracks)
        {
            _trackRepository.InsertMany(tracks);
        }

        public void UpdateMany(List<Track> tracks)
        {
            _trackRepository.UpdateMany(tracks);
        }

        public void DeleteMany(List<Track> tracks)
        {
            _trackRepository.DeleteMany(tracks);
        }

        public void HandleAsync(ArtistDeletedEvent message)
        {
            var tracks = GetTracksByArtist(message.Artist.Id);
            _trackRepository.DeleteMany(tracks);
        }

        public void HandleAsync(AlbumDeletedEvent message)
        {
            var tracks = GetTracksByAlbum(message.Album.Id);
            _trackRepository.DeleteMany(tracks);
        }

        public void HandleAsync(ReleaseDeletedEvent message)
        {
            var tracks = GetTracksByRelease(message.Release.Id);
            _trackRepository.DeleteMany(tracks);
        }

        public void Handle(TrackFileDeletedEvent message)
        {
            foreach (var track in GetTracksByFileId(message.TrackFile.Id))
            {
                _logger.Debug("Detaching track {0} from file.", track.Id);
                track.TrackFileId = 0;

                // if (message.Reason != DeleteMediaFileReason.Upgrade && _configService.AutoUnmonitorPreviouslyDownloadedTracks)
                // {
                //     track.Monitored = false;
                // }

                UpdateTrack(track);
            }
        }

        public void Handle(TrackFileAddedEvent message)
        {
            foreach (var track in message.TrackFile.Tracks.Value)
            {
                _trackRepository.SetFileId(track.Id, message.TrackFile.Id);
                _logger.Debug("Linking [{0}] > [{1}]", message.TrackFile.RelativePath, track);
            }
        }
    }
}
