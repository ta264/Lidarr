using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.Music.Events;

namespace NzbDrone.Core.Music
{
    public class AlbumEditedService : IHandle<AlbumEditedEvent>
    {
        private readonly IManageCommandQueue _commandQueueManager;

        public AlbumEditedService(IManageCommandQueue commandQueueManager)
        {
            _commandQueueManager = commandQueueManager;
        }

        public void Handle(AlbumEditedEvent message)
        {
            if (message.Album.SelectedReleaseId != message.OldAlbum.SelectedReleaseId)
            {
                _commandQueueManager.Push(new RescanArtistCommand(message.Album.ArtistId));
            }
        }
    }
}
