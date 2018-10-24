using NLog;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Music.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using Marr.Data;

namespace NzbDrone.Core.Music
{
    public interface IReleaseService
    {
        Release GetRelease(int id);
        void InsertMany(List<Release> releases);
        void UpdateMany(List<Release> releases);
        void DeleteMany(List<Release> releases);
        List<Release> GetReleasesByReleaseGroup(int releaseGroupId);
    }

    public class ReleaseService : IReleaseService,
                                  IHandleAsync<AlbumDeletedEvent>
    {
        private readonly IReleaseRepository _releaseRepository;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public ReleaseService(IReleaseRepository releaseRepository,
                              IEventAggregator eventAggregator,
                              Logger logger)
        {
            _releaseRepository = releaseRepository;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public Release GetRelease(int id)
        {
            return _releaseRepository.Get(id);
        }

        public void InsertMany(List<Release> releases)
        {
            _releaseRepository.InsertMany(releases);
        }

        public void UpdateMany(List<Release> releases)
        {
            _releaseRepository.UpdateMany(releases);
        }

        public void DeleteMany(List<Release> releases)
        {
            _releaseRepository.DeleteMany(releases);
            foreach (var release in releases)
            {
                _eventAggregator.PublishEvent(new ReleaseDeletedEvent(release));
            }
        }

        public List<Release> GetReleasesByReleaseGroup(int releaseGroupId)
        {
            return _releaseRepository.FindByReleaseGroup(releaseGroupId);
        }

        public void HandleAsync(AlbumDeletedEvent message)
        {
            var releases = GetReleasesByReleaseGroup(message.Album.Id);
            _releaseRepository.DeleteMany(releases);
        }

    }
}
